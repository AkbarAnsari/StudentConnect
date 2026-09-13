using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Data.Stores;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StudentsController(StudentStore students, ReferenceDataStore referenceData, SectionStore sections, ICurrentUserService currentUser, IAuditService audit, ILogger<StudentsController> logger) : ControllerBase
{
    // GET /api/students
    [HttpGet]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<IEnumerable<StudentResponseDto>>> GetStudents()
    {
        var all = await students.GetAllAsync();
        return Ok(all.Select(StudentResponseDto.FromEntity));
    }

    // GET /api/students/{id} — a Student may only view their own profile; Teacher/Admin may view any.
    [HttpGet("{id:int}")]
    public async Task<ActionResult<StudentResponseDto>> GetStudentById(int id)
    {
        if (!currentUser.CanAccessStudent(id))
        {
            return Forbid();
        }

        var student = await students.GetByIdAsync(id);

        if (student is null)
        {
            return NotFound();
        }

        return Ok(StudentResponseDto.FromEntity(student));
    }

    // POST /api/students
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<StudentResponseDto>> CreateStudent(StudentCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.StudentId) ||
            string.IsNullOrWhiteSpace(dto.FirstName) ||
            string.IsNullOrWhiteSpace(dto.LastName) ||
            string.IsNullOrWhiteSpace(dto.Email))
        {
            return BadRequest(new { message = "StudentId, FirstName, LastName, and Email are required." });
        }

        var gradeExists = await referenceData.GradeExistsAsync(dto.GradeId);
        if (!gradeExists)
        {
            return BadRequest(new { message = "The specified GradeId does not exist." });
        }

        if (!await sections.BelongsToGradeAsync(dto.SectionId, dto.GradeId))
        {
            return BadRequest(new { message = "The specified SectionId does not belong to the selected grade." });
        }

        var duplicate = await students.StudentCodeExistsAsync(dto.StudentId);
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

        var created = await students.GetByIdAsync(newId);
        var result = StudentResponseDto.FromEntity(created!);
        await audit.LogAsync("Create", "Student", newId.ToString(), $"{dto.StudentId} {dto.FirstName} {dto.LastName}");
        return CreatedAtAction(nameof(GetStudentById), new { id = newId }, result);
    }

    // PUT /api/students/{id}
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<StudentResponseDto>> UpdateStudent(int id, StudentUpdateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FirstName) ||
            string.IsNullOrWhiteSpace(dto.LastName) ||
            string.IsNullOrWhiteSpace(dto.Email))
        {
            return BadRequest(new { message = "FirstName, LastName, and Email are required." });
        }

        if (!await students.ExistsAsync(id))
        {
            return NotFound();
        }

        var gradeExists = await referenceData.GradeExistsAsync(dto.GradeId);
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

        var updated = await students.GetByIdAsync(id);
        await audit.LogAsync("Update", "Student", id.ToString(), $"{dto.FirstName} {dto.LastName}, GradeId={dto.GradeId}, SectionId={dto.SectionId}");
        return Ok(StudentResponseDto.FromEntity(updated!));
    }

    // DELETE /api/students/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteStudent(int id)
    {
        if (!await students.ExistsAsync(id))
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
}

