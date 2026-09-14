using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Data.Stores;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/sections")]
public class SectionsController(SectionStore sections, ReferenceDataStore referenceData, StudentStore students, ICurrentUserService currentUser, ICurrentTenantService currentTenant, ILogger<SectionsController> logger) : ControllerBase
{
    // GET /api/sections?includeArchived=true
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IEnumerable<SectionDto>>> GetSections(bool includeArchived = false)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        var all = await sections.GetAllAsync(schoolId, includeArchived);
        return Ok(all.Select(SectionDto.FromEntity));
    }

    // GET /api/sections/grade/{gradeId} — used by the Add/Edit Student forms' Section dropdown.
    [HttpGet("grade/{gradeId:int}")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<SectionDto>>> GetByGrade(int gradeId)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        if (!await referenceData.GradeExistsAsync(gradeId, schoolId))
        {
            return NotFound(new { message = "Grade not found." });
        }

        var byGrade = await sections.GetByGradeAsync(gradeId);
        return Ok(byGrade.Select(SectionDto.FromEntity));
    }

    // GET /api/sections/my — a Teacher's homeroom sections, for the "My Classes" roster view.
    [HttpGet("my")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<IEnumerable<SectionDto>>> GetMySections()
    {
        if (currentUser.TeacherId is not { } teacherId)
        {
            return Forbid();
        }

        var mine = await sections.GetForTeacherAsync(teacherId);
        return Ok(mine.Select(SectionDto.FromEntity));
    }

    // GET /api/sections/{id}/students — a homeroom teacher's class roster, or any section for Admin.
    [HttpGet("{id:int}/students")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<IEnumerable<SectionStudentDto>>> GetStudentsInSection(int id)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        var section = await sections.GetByIdAsync(id, schoolId);
        if (section is null)
        {
            return NotFound();
        }

        if (currentUser.Role == "Teacher" && section.HomeroomTeacherId != currentUser.TeacherId)
        {
            return Forbid();
        }

        var roster = await students.GetBySectionAsync(id);
        return Ok(roster.Select(SectionStudentDto.FromEntity));
    }

    // POST /api/sections
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<SectionDto>> CreateSection(SectionCreateDto dto)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return BadRequest(new { message = "Name is required." });
        }

        if (!await referenceData.GradeExistsAsync(dto.GradeId, schoolId))
        {
            return BadRequest(new { message = "Grade does not exist." });
        }

        if (dto.HomeroomTeacherId is { } teacherId && !await referenceData.TeacherExistsAsync(teacherId))
        {
            return BadRequest(new { message = "The specified HomeroomTeacherId does not exist." });
        }

        if (await sections.NameExistsInGradeAsync(dto.GradeId, dto.Name))
        {
            return Conflict(new { message = $"Section '{dto.Name}' already exists for this grade." });
        }

        int newId;
        try
        {
            newId = await sections.CreateAsync(new Section { GradeId = dto.GradeId, Name = dto.Name, HomeroomTeacherId = dto.HomeroomTeacherId });
        }
        catch (Npgsql.PostgresException ex)
        {
            logger.LogError(ex, "Failed to save section {Name}", dto.Name);
            return StatusCode(500, new { message = "Unable to save the section. Please try again." });
        }

        var byGrade = await sections.GetByGradeAsync(dto.GradeId);
        var created = byGrade.First(s => s.Id == newId);
        return CreatedAtAction(nameof(GetSections), null, SectionDto.FromEntity(created));
    }

    // DELETE /api/sections/{id} — archives (soft-deletes) rather than physically deleting, so a
    // closed section's historical attendance/marks/homework stay resolvable.
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteSection(int id)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        if (!await sections.ExistsAsync(id, schoolId))
        {
            return NotFound();
        }

        await sections.ArchiveAsync(id);
        return NoContent();
    }

    // POST /api/sections/{id}/restore
    [HttpPost("{id:int}/restore")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RestoreSection(int id)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        if (!await sections.ExistsAsync(id, schoolId))
        {
            return NotFound();
        }

        await sections.RestoreAsync(id);
        return NoContent();
    }
}
