using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Security.Claims;
using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Data.Stores;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Teacher,Admin")]
public class AssessmentsController(
    AssessmentStore assessments,
    ReferenceDataStore referenceData,
    TermStore terms,
    ICurrentTenantService currentTenant,
    ILogger<AssessmentsController> logger,
    MarkStore? marks = null) : ControllerBase
{
    // GET /api/assessments
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AssessmentDto>>> GetAssessments()
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        var all = await assessments.GetAllAsync(schoolId);
        if (User.IsInRole("Teacher") && GetTeacherId() is { } teacherId)
        {
            all = all.Where(assessment =>
                assessment.TeacherSubjectAssignmentId is null ||
                assessment.TeacherSubjectAssignment?.TeacherId == teacherId).ToList();
        }
        return Ok(all.Select(AssessmentDto.FromEntity));
    }

    // GET /api/assessments/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<AssessmentDto>> GetAssessmentById(int id)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        var assessment = await assessments.GetByIdAsync(id, schoolId);

        if (assessment is null)
        {
            return NotFound();
        }

        if (!CanViewAssessment(assessment))
        {
            return NotFound();
        }

        return Ok(AssessmentDto.FromEntity(assessment));
    }

    // GET /api/assessments/subject/{subjectId}
    [HttpGet("subject/{subjectId:int}")]
    public async Task<ActionResult<IEnumerable<AssessmentDto>>> GetAssessmentsBySubject(int subjectId)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        if (!await referenceData.SubjectExistsAsync(subjectId, schoolId))
        {
            return NotFound(new { message = "Subject not found." });
        }

        var bySubject = await assessments.GetBySubjectAsync(subjectId, schoolId);
        if (User.IsInRole("Teacher") && GetTeacherId() is { } teacherId)
        {
            bySubject = bySubject.Where(assessment =>
                assessment.TeacherSubjectAssignmentId is null ||
                assessment.TeacherSubjectAssignment?.TeacherId == teacherId).ToList();
        }
        return Ok(bySubject.Select(AssessmentDto.FromEntity));
    }

    // POST /api/assessments
    [HttpPost]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<AssessmentDto>> CreateAssessment(AssessmentCreateDto dto)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        TeacherSubjectAssignment? assignment = null;
        if (dto.TeacherSubjectAssignmentId is { } assignmentId)
        {
            assignment = await assessments.GetAssignmentAsync(assignmentId, schoolId);
            if (assignment is null)
            {
                return BadRequest(new { message = "Assigned class does not exist." });
            }

            if (!CanManageAssignment(assignment.TeacherId))
            {
                return Forbid();
            }
        }
        else if (User.Identity?.IsAuthenticated == true)
        {
            return BadRequest(new { message = "Select an assigned class for this assessment." });
        }

        var subjectId = assignment?.SubjectId ?? dto.SubjectId;
        var validation = await ValidateAsync(dto.Title, subjectId, dto.MaximumMarks, dto.AssessmentType, schoolId);
        if (validation is not null)
        {
            return validation;
        }

        var termValidation = await ValidateTermAsync(dto.TermId);
        if (termValidation is not null)
        {
            return termValidation;
        }

        Enum.TryParse<AssessmentType>(dto.AssessmentType, ignoreCase: true, out var type);

        var assessment = new Assessment
        {
            Title = dto.Title,
            SubjectId = subjectId,
            Date = dto.Date,
            MaximumMarks = dto.MaximumMarks,
            AssessmentType = type,
            Description = dto.Description,
            TermId = dto.TermId,
            Weight = dto.Weight <= 0 ? 1.0m : dto.Weight,
            Status = dto.Publish ? AssessmentStatus.Published : AssessmentStatus.Draft,
            PublishedAtUtc = dto.Publish ? DateTime.UtcNow : null,
            TeacherSubjectAssignmentId = assignment?.Id,
        };

        int newId;
        try
        {
            newId = await assessments.CreateAsync(assessment);
        }
        catch (PostgresException ex)
        {
            logger.LogError(ex, "Failed to save assessment {Title}", dto.Title);
            return StatusCode(500, new { message = "Unable to save the assessment. Please try again." });
        }

        var created = await assessments.GetByIdAsync(newId, schoolId);
        var result = AssessmentDto.FromEntity(created!);
        return CreatedAtAction(nameof(GetAssessmentById), new { id = newId }, result);
    }

    // PUT /api/assessments/{id}
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<AssessmentDto>> UpdateAssessment(int id, AssessmentUpdateDto dto)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        var existing = await assessments.GetByIdAsync(id, schoolId);
        if (existing is null)
        {
            return NotFound();
        }

        if (!CanManageAssessment(existing))
        {
            return Forbid();
        }

        if (existing.Status == AssessmentStatus.Published)
        {
            return Conflict(new { message = "Reopen this assessment to Draft before editing it." });
        }

        var maximumRecorded = marks is null ? 0 : await marks.GetMaximumRecordedAsync(id);
        if (dto.MaximumMarks < maximumRecorded)
            return BadRequest(new { message = $"Maximum marks cannot be less than the highest recorded score ({maximumRecorded})." });

        var subjectId = existing.TeacherSubjectAssignment?.SubjectId ?? dto.SubjectId;
        var validation = await ValidateAsync(dto.Title, subjectId, dto.MaximumMarks, dto.AssessmentType, schoolId);
        if (validation is not null)
        {
            return validation;
        }

        var termValidation = await ValidateTermAsync(dto.TermId);
        if (termValidation is not null)
        {
            return termValidation;
        }

        if (existing.TermId is not null && await terms.IsLockedAsync(existing.TermId.Value))
        {
            return Conflict(new { message = "This assessment's term is locked and can no longer be modified." });
        }

        Enum.TryParse<AssessmentType>(dto.AssessmentType, ignoreCase: true, out var type);

        var assessment = new Assessment
        {
            Id = id,
            Title = dto.Title,
            SubjectId = subjectId,
            Date = dto.Date,
            MaximumMarks = dto.MaximumMarks,
            AssessmentType = type,
            Description = dto.Description,
            TermId = dto.TermId,
            Weight = dto.Weight <= 0 ? 1.0m : dto.Weight,
            TeacherSubjectAssignmentId = existing.TeacherSubjectAssignmentId,
        };

        try
        {
            await assessments.UpdateAsync(assessment);
        }
        catch (PostgresException ex)
        {
            logger.LogError(ex, "Failed to update assessment {Id}", id);
            return StatusCode(500, new { message = "Unable to update the assessment. Please try again." });
        }

        var updated = await assessments.GetByIdAsync(id, schoolId);
        return Ok(AssessmentDto.FromEntity(updated!));
    }

    // DELETE /api/assessments/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<IActionResult> DeleteAssessment(int id)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        var existing = await assessments.GetByIdAsync(id, schoolId);
        if (existing is null)
        {
            return NotFound();
        }

        if (!CanManageAssessment(existing))
        {
            return Forbid();
        }

        if (existing.Status == AssessmentStatus.Published)
        {
            return Conflict(new { message = "Reopen this assessment to Draft before deleting it." });
        }

        if (existing.TermId is not null && await terms.IsLockedAsync(existing.TermId.Value))
        {
            return Conflict(new { message = "This assessment's term is locked and can no longer be deleted." });
        }

        try
        {
            await assessments.DeleteAsync(id);
        }
        catch (PostgresException ex)
        {
            logger.LogError(ex, "Failed to delete assessment {Id}: {Message}", id, ex.Message);
            return Conflict(new { message = "Cannot delete an assessment that already has marks recorded against it." });
        }

        return NoContent();
    }

    [HttpPost("{id:int}/publish")]
    public async Task<ActionResult<AssessmentDto>> PublishAssessment(int id)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        var assessment = await assessments.GetByIdAsync(id, schoolId);
        if (assessment is null)
        {
            return NotFound();
        }

        if (!CanManageAssessment(assessment))
        {
            return Forbid();
        }

        if (assessment.TermId is not null && await terms.IsLockedAsync(assessment.TermId.Value))
        {
            return Conflict(new { message = "This assessment's term is locked and its publication state cannot change." });
        }

        await assessments.SetStatusAsync(id, AssessmentStatus.Published);
        return Ok(AssessmentDto.FromEntity((await assessments.GetByIdAsync(id, schoolId))!));
    }

    [HttpPost("{id:int}/reopen")]
    public async Task<ActionResult<AssessmentDto>> ReopenAssessment(int id)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        var assessment = await assessments.GetByIdAsync(id, schoolId);
        if (assessment is null)
        {
            return NotFound();
        }

        if (!CanManageAssessment(assessment))
        {
            return Forbid();
        }

        if (assessment.TermId is not null && await terms.IsLockedAsync(assessment.TermId.Value))
        {
            return Conflict(new { message = "This assessment's term is locked and its publication state cannot change." });
        }

        await assessments.SetStatusAsync(id, AssessmentStatus.Draft);
        return Ok(AssessmentDto.FromEntity((await assessments.GetByIdAsync(id, schoolId))!));
    }

    private async Task<ActionResult?> ValidateTermAsync(int? termId)
    {
        if (termId is null)
        {
            return null;
        }

        if (currentTenant.SchoolId is not { } schoolId || !await terms.ExistsAsync(termId.Value, schoolId))
        {
            return BadRequest(new { message = "Term does not exist." });
        }

        if (await terms.IsLockedAsync(termId.Value))
        {
            return Conflict(new { message = "This term is locked; new assessments cannot be added to it." });
        }

        return null;
    }

    private bool CanManageAssessment(Assessment assessment) =>
        assessment.TeacherSubjectAssignmentId is null
            ? !User.IsInRole("Teacher")
            : CanManageAssignment(assessment.TeacherSubjectAssignment!.TeacherId);

    private bool CanViewAssessment(Assessment assessment) =>
        !User.IsInRole("Teacher") ||
        assessment.TeacherSubjectAssignmentId is null ||
        assessment.TeacherSubjectAssignment?.TeacherId == GetTeacherId();

    private bool CanManageAssignment(int teacherId) =>
        User.Identity?.IsAuthenticated != true || User.IsInRole("Admin") || GetTeacherId() == teacherId;

    private int? GetTeacherId() =>
        int.TryParse(User.FindFirstValue("TeacherId"), out var teacherId) ? teacherId : null;

    private async Task<ActionResult?> ValidateAsync(string title, int subjectId, int maximumMarks, string assessmentType, int schoolId)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return BadRequest(new { message = "Title is required." });
        }

        if (maximumMarks <= 0)
        {
            return BadRequest(new { message = "MaximumMarks must be greater than zero." });
        }

        if (!Enum.TryParse<AssessmentType>(assessmentType, ignoreCase: true, out _))
        {
            return BadRequest(new { message = $"AssessmentType '{assessmentType}' is not valid." });
        }

        if (!await referenceData.SubjectExistsAsync(subjectId, schoolId))
        {
            return BadRequest(new { message = "Subject does not exist." });
        }

        return null;
    }
}
