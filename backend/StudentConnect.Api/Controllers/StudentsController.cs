using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Text;
using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Data.Stores;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StudentsController(StudentStore students, ReferenceDataStore referenceData, SectionStore sections, ICurrentUserService currentUser, ICurrentTenantService currentTenant, IAuditService audit, ILogger<StudentsController> logger) : ControllerBase
{
    // GET /api/students?status=Active|Withdrawn|Graduated|Transferred|All&gradeId=&page=&pageSize= —
    // status defaults to Active only; page/pageSize default to the first 50.
    [HttpGet]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<PagedResult<StudentResponseDto>>> GetStudents(string? status, int? gradeId, int page = 1, int pageSize = 50)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        StudentStatus? statusFilter = string.IsNullOrWhiteSpace(status)
            ? StudentStatus.Active
            : string.Equals(status, "All", StringComparison.OrdinalIgnoreCase)
                ? null
                : Enum.TryParse<StudentStatus>(status, true, out var parsed) ? parsed : StudentStatus.Active;

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var (items, totalCount) = await students.GetPagedAsync(schoolId, statusFilter, page, pageSize, gradeId);
        return Ok(new PagedResult<StudentResponseDto>
        {
            Items = items.Select(StudentResponseDto.FromEntity).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
        });
    }

    // GET /api/students/{id} — a Student may only view their own profile; Teacher/Admin may view any.
    [HttpGet("{id:int}")]
    public async Task<ActionResult<StudentResponseDto>> GetStudentById(int id)
    {
        if (!currentUser.CanAccessStudent(id))
        {
            return Forbid();
        }

        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        var student = await students.GetByIdAsync(id, schoolId);

        if (student is null)
        {
            return NotFound();
        }

        return Ok(StudentResponseDto.FromEntity(student));
    }

    // GET /api/students/{id}/subjects — the student's actual enrolled subjects (StudentSubjects
    // join), replacing the frontend's previous hardcoded subject list.
    [HttpGet("{id:int}/subjects")]
    public async Task<ActionResult<IEnumerable<StudentSubjectDto>>> GetStudentSubjects(int id)
    {
        if (!currentUser.CanAccessStudent(id))
        {
            return Forbid();
        }

        if (currentTenant.SchoolId is not { } schoolId || !await students.ExistsAsync(id, schoolId))
        {
            return NotFound();
        }

        var subjectIds = await students.GetSubjectIdsForStudentAsync(id);
        var subjects = await referenceData.GetSubjectsByIdsAsync(subjectIds, schoolId);
        return Ok(subjects.Select(s => new StudentSubjectDto { Id = s.Id, Name = s.Name, Code = s.Code }));
    }

    // POST /api/students
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<StudentResponseDto>> CreateStudent(StudentCreateDto dto)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(dto.StudentId) ||
            string.IsNullOrWhiteSpace(dto.FirstName) ||
            string.IsNullOrWhiteSpace(dto.LastName) ||
            string.IsNullOrWhiteSpace(dto.Email))
        {
            return BadRequest(new { message = "StudentId, FirstName, LastName, and Email are required." });
        }

        var gradeExists = await referenceData.GradeExistsAsync(dto.GradeId, schoolId);
        if (!gradeExists)
        {
            return BadRequest(new { message = "The specified GradeId does not exist." });
        }

        if (!await sections.BelongsToGradeAsync(dto.SectionId, dto.GradeId))
        {
            return BadRequest(new { message = "The specified SectionId does not belong to the selected grade." });
        }

        var duplicate = await students.StudentCodeExistsAsync(dto.StudentId, schoolId);
        if (duplicate)
        {
            return Conflict(new { message = $"A student with StudentId '{dto.StudentId}' already exists." });
        }

        var student = new Student
        {
            StudentId = dto.StudentId,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            GradeId = dto.GradeId,
            SectionId = dto.SectionId,
            SchoolId = schoolId,
            DateOfBirth = dto.DateOfBirth,
            Phone = dto.Phone,
            Address = dto.Address,
        };

        int newId;
        try
        {
            newId = await students.CreateAsync(student);
        }
        catch (PostgresException ex)
        {
            logger.LogError(ex, "Failed to save new student {StudentId}", dto.StudentId);
            return StatusCode(500, new { message = "Unable to save the student. Please try again." });
        }

        var created = await students.GetByIdAsync(newId, schoolId);
        var result = StudentResponseDto.FromEntity(created!);
        await audit.LogAsync("Create", "Student", newId.ToString(), $"{dto.StudentId} {dto.FirstName} {dto.LastName}");
        return CreatedAtAction(nameof(GetStudentById), new { id = newId }, result);
    }

    // PUT /api/students/{id}
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<StudentResponseDto>> UpdateStudent(int id, StudentUpdateDto dto)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(dto.FirstName) ||
            string.IsNullOrWhiteSpace(dto.LastName) ||
            string.IsNullOrWhiteSpace(dto.Email))
        {
            return BadRequest(new { message = "FirstName, LastName, and Email are required." });
        }

        if (!await students.ExistsAsync(id, schoolId))
        {
            return NotFound();
        }

        var gradeExists = await referenceData.GradeExistsAsync(dto.GradeId, schoolId);
        if (!gradeExists)
        {
            return BadRequest(new { message = "The specified GradeId does not exist." });
        }

        if (!await sections.BelongsToGradeAsync(dto.SectionId, dto.GradeId))
        {
            return BadRequest(new { message = "The specified SectionId does not belong to the selected grade." });
        }

        var student = new Student
        {
            Id = id,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            GradeId = dto.GradeId,
            SectionId = dto.SectionId,
            DateOfBirth = dto.DateOfBirth,
            Phone = dto.Phone,
            Address = dto.Address,
        };

        try
        {
            await students.UpdateAsync(student);
        }
        catch (PostgresException ex)
        {
            logger.LogError(ex, "Failed to update student {Id}", id);
            return StatusCode(500, new { message = "Unable to update the student. Please try again." });
        }

        var updated = await students.GetByIdAsync(id, schoolId);
        await audit.LogAsync("Update", "Student", id.ToString(), $"{dto.FirstName} {dto.LastName}, GradeId={dto.GradeId}, SectionId={dto.SectionId}");
        return Ok(StudentResponseDto.FromEntity(updated!));
    }

    // DELETE /api/students/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteStudent(int id)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        if (!await students.ExistsAsync(id, schoolId))
        {
            return NotFound();
        }

        try
        {
            await students.DeleteAsync(id);
        }
        catch (PostgresException ex)
        {
            logger.LogError(ex, "Failed to delete student {Id}", id);
            return StatusCode(500, new { message = "Unable to delete the student. Please try again." });
        }

        await audit.LogAsync("Delete", "Student", id.ToString());
        return NoContent();
    }

    // PUT /api/students/{id}/status — the supported alternative to deleting a student: their
    // history (attendance/marks/homework) stays intact and queryable.
    [HttpPut("{id:int}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<StudentResponseDto>> UpdateStudentStatus(int id, StudentStatusUpdateDto dto)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        var existing = await students.GetByIdAsync(id, schoolId);
        if (existing is null)
        {
            return NotFound();
        }

        if (!Enum.TryParse<StudentStatus>(dto.Status, true, out var newStatus))
        {
            return BadRequest(new { message = "Status must be one of Active, Withdrawn, Graduated, Transferred." });
        }

        if (newStatus != StudentStatus.Active && string.IsNullOrWhiteSpace(dto.Reason))
        {
            return BadRequest(new { message = "Reason is required when changing status away from Active." });
        }

        await students.UpdateStatusAsync(id, newStatus, dto.Reason);
        await audit.LogAsync(
            "StatusChange",
            "Student",
            id.ToString(),
            $"{existing.Status} -> {newStatus}" + (string.IsNullOrWhiteSpace(dto.Reason) ? string.Empty : $" ({dto.Reason})"));

        var updated = await students.GetByIdAsync(id, schoolId);
        return Ok(StudentResponseDto.FromEntity(updated!));
    }

    // POST /api/students/import — bulk CSV upload. Header row must include StudentId,
    // FirstName, LastName, Email, GradeName (SectionName is optional). Existing StudentIds
    // and invalid rows are skipped and reported, not overwritten.
    [HttpPost("import")]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<ActionResult<StudentImportResultDto>> ImportStudents(IFormFile file)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        if (file is not { Length: > 0 })
        {
            return BadRequest(new { message = "Please select a CSV file to upload." });
        }

        List<string> lines;
        using (var reader = new StreamReader(file.OpenReadStream()))
        {
            lines = (await reader.ReadToEndAsync())
                .Split('\n')
                .Select(l => l.TrimEnd('\r'))
                .Where(l => l.Length > 0)
                .ToList();
        }

        if (lines.Count == 0)
        {
            return BadRequest(new { message = "The file is empty." });
        }

        var header = ParseCsvLine(lines[0]).Select(h => h.Trim().ToLowerInvariant()).ToList();
        var requiredColumns = new[] { "studentid", "firstname", "lastname", "email", "gradename" };
        if (requiredColumns.Any(c => !header.Contains(c)))
        {
            return BadRequest(new { message = "The CSV header must include StudentId, FirstName, LastName, Email, GradeName (SectionName is optional)." });
        }

        var studentIdCol = header.IndexOf("studentid");
        var firstNameCol = header.IndexOf("firstname");
        var lastNameCol = header.IndexOf("lastname");
        var emailCol = header.IndexOf("email");
        var gradeNameCol = header.IndexOf("gradename");
        var sectionNameCol = header.IndexOf("sectionname");

        var grades = await referenceData.GetAllGradesAsync(schoolId);
        var gradesByName = grades.ToDictionary(g => g.Name, g => g, StringComparer.OrdinalIgnoreCase);
        var allSections = await sections.GetAllAsync(schoolId);

        var result = new StudentImportResultDto { TotalRows = lines.Count - 1 };
        var seenStudentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 1; i < lines.Count; i++)
        {
            var rowNumber = i + 1;
            var fields = ParseCsvLine(lines[i]);

            string Get(int col) => col >= 0 && col < fields.Count ? fields[col].Trim() : string.Empty;

            var studentCode = Get(studentIdCol);
            var firstName = Get(firstNameCol);
            var lastName = Get(lastNameCol);
            var email = Get(emailCol);
            var gradeName = Get(gradeNameCol);
            var sectionName = sectionNameCol >= 0 ? Get(sectionNameCol) : string.Empty;

            if (string.IsNullOrWhiteSpace(studentCode) || string.IsNullOrWhiteSpace(firstName) ||
                string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(gradeName))
            {
                result.Errors.Add(new StudentImportRowResult { Row = rowNumber, Message = "StudentId, FirstName, LastName, Email, and GradeName are all required.", StudentId = studentCode });
                continue;
            }

            if (!seenStudentIds.Add(studentCode))
            {
                result.Errors.Add(new StudentImportRowResult { Row = rowNumber, Message = $"Duplicate StudentId '{studentCode}' within the file.", StudentId = studentCode });
                continue;
            }

            if (!gradesByName.TryGetValue(gradeName, out var grade))
            {
                result.Errors.Add(new StudentImportRowResult { Row = rowNumber, Message = $"Grade '{gradeName}' does not exist.", StudentId = studentCode });
                continue;
            }

            int? sectionId = null;
            if (!string.IsNullOrWhiteSpace(sectionName))
            {
                var section = allSections.FirstOrDefault(s => s.GradeId == grade.Id && string.Equals(s.Name, sectionName, StringComparison.OrdinalIgnoreCase));
                if (section is null)
                {
                    result.Errors.Add(new StudentImportRowResult { Row = rowNumber, Message = $"Section '{sectionName}' does not exist in grade '{gradeName}'.", StudentId = studentCode });
                    continue;
                }
                sectionId = section.Id;
            }

            if (await students.StudentCodeExistsAsync(studentCode, schoolId))
            {
                result.Errors.Add(new StudentImportRowResult { Row = rowNumber, Message = $"A student with StudentId '{studentCode}' already exists.", StudentId = studentCode });
                continue;
            }

            try
            {
                var newId = await students.CreateAsync(new Student
                {
                    StudentId = studentCode,
                    FirstName = firstName,
                    LastName = lastName,
                    Email = email,
                    GradeId = grade.Id,
                    SectionId = sectionId,
                    SchoolId = schoolId,
                });
                await audit.LogAsync("Create", "Student", newId.ToString(), $"Bulk import: {studentCode} {firstName} {lastName}");
                result.SuccessCount++;
            }
            catch (PostgresException ex)
            {
                logger.LogError(ex, "Failed to import student row {Row}", rowNumber);
                result.Errors.Add(new StudentImportRowResult { Row = rowNumber, Message = "Unable to save this row. Please try again.", StudentId = studentCode });
            }
        }

        return Ok(result);
    }

    // Minimal RFC4180-style parser — handles quoted fields with embedded commas/quotes,
    // which is all this import needs (no multi-line quoted fields).
    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields;
    }
}

