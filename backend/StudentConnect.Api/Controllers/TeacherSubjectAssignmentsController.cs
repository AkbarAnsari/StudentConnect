using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentConnect.Application;
using StudentConnect.Infrastructure.Data.Stores;

namespace StudentConnect.Api.Controllers;

// Formal, admin-managed record of which Teacher teaches which Subject in which Section —
// additive only: existing homework/assessment/attendance endpoints are unchanged and still
// rely on their own existing role/ownership checks, not on this table.
[ApiController]
[Route("api/teacher-subject-assignments")]
public class TeacherSubjectAssignmentsController(
    TeacherSubjectAssignmentStore assignments,
    TeacherStore teachers,
    SectionStore sections,
    ReferenceDataStore referenceData,
    StudentStore students,
    ICurrentUserService currentUser,
    ICurrentTenantService currentTenant,
    IAuditService audit) : ControllerBase
{
    // GET /api/teacher-subject-assignments
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<TeacherSubjectAssignmentDto>>> GetAll()
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        var all = await assignments.GetAllAsync(schoolId);
        return Ok(all.Select(TeacherSubjectAssignmentDto.FromEntity));
    }

    // GET /api/teacher-subject-assignments/my — a Teacher's own assignments.
    [HttpGet("my")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<IEnumerable<TeacherSubjectAssignmentDto>>> GetMine()
    {
        if (currentUser.TeacherId is not { } teacherId)
        {
            return Forbid();
        }

        var mine = await assignments.GetForTeacherAsync(teacherId);
        return Ok(mine.Select(TeacherSubjectAssignmentDto.FromEntity));
    }

    [HttpGet("{id:int}/students")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<IEnumerable<SectionStudentDto>>> GetStudents(int id)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        var assignment = await assignments.GetByIdAsync(id, schoolId);
        if (assignment is null)
        {
            return NotFound();
        }

        if (currentUser.Role == "Teacher" && assignment.TeacherId != currentUser.TeacherId)
        {
            return Forbid();
        }

        var roster = await students.GetActiveBySectionAndSubjectAsync(assignment.SectionId, assignment.SubjectId, schoolId);
        return Ok(roster.Select(SectionStudentDto.FromEntity));
    }

    // POST /api/teacher-subject-assignments
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<TeacherSubjectAssignmentDto>> Create(TeacherSubjectAssignmentCreateDto dto)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        if (!await teachers.ExistsAsync(dto.TeacherId, schoolId))
        {
            return BadRequest(new { message = "Teacher does not exist." });
        }

        if (!await referenceData.SubjectExistsAsync(dto.SubjectId, schoolId))
        {
            return BadRequest(new { message = "Subject does not exist." });
        }

        if (!await sections.ExistsAsync(dto.SectionId, schoolId))
        {
            return BadRequest(new { message = "Section does not exist." });
        }

        if (await assignments.ExistsAsync(dto.TeacherId, dto.SubjectId, dto.SectionId))
        {
            return Conflict(new { message = "This teacher is already assigned to this subject in this section." });
        }

        var newId = await assignments.CreateAsync(dto.TeacherId, dto.SubjectId, dto.SectionId);
        var created = await assignments.GetByIdAsync(newId, schoolId);
        await audit.LogAsync("Create", "TeacherSubjectAssignment", newId.ToString(), $"TeacherId={dto.TeacherId}, SubjectId={dto.SubjectId}, SectionId={dto.SectionId}");
        return CreatedAtAction(nameof(GetAll), null, TeacherSubjectAssignmentDto.FromEntity(created!));
    }

    // DELETE /api/teacher-subject-assignments/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        if (!await assignments.ExistsAsync(id, schoolId))
        {
            return NotFound();
        }

        await assignments.DeleteAsync(id);
        await audit.LogAsync("Delete", "TeacherSubjectAssignment", id.ToString());
        return NoContent();
    }
}
