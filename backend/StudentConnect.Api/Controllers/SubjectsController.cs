using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Data.Stores;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubjectsController(ReferenceDataStore referenceData, ICurrentTenantService currentTenant, ILogger<SubjectsController> logger) : ControllerBase
{
    // GET /api/subjects — used by the (temporary, unauthenticated) Record Attendance page.
    // Tenant-scoped when the caller is authenticated; falls back to the pre-existing unscoped
    // list for the anonymous case, unchanged from before multi-tenancy.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Subject>>> GetSubjects()
    {
        var subjects = currentTenant.SchoolId is { } schoolId
            ? await referenceData.GetAllSubjectsAsync(schoolId)
            : await referenceData.GetAllSubjectsAsync();
        return Ok(subjects);
    }

    // POST /api/subjects
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Subject>> CreateSubject(SubjectCreateDto dto)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Code))
        {
            return BadRequest(new { message = "Name and Code are required." });
        }

        if (await referenceData.SubjectCodeExistsAsync(dto.Code, schoolId))
        {
            return Conflict(new { message = $"Subject code '{dto.Code}' already exists." });
        }

        var newId = await referenceData.CreateSubjectAsync(dto.Name, dto.Code, schoolId);
        var created = await referenceData.GetSubjectByIdAsync(newId, schoolId);
        return CreatedAtAction(nameof(GetSubjects), null, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Subject>> UpdateSubject(int id, SubjectUpdateDto dto)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        if (!await referenceData.SubjectExistsAsync(id, schoolId))
        {
            return NotFound();
        }

        var name = (dto.Name ?? string.Empty).Trim();
        var code = (dto.Code ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code))
        {
            return BadRequest(new { message = "Name and Code are required." });
        }

        if (await referenceData.SubjectCodeExistsAsync(code, schoolId, id))
        {
            return Conflict(new { message = $"Subject code '{code}' already exists." });
        }

        await referenceData.UpdateSubjectAsync(id, name, code, schoolId);
        return Ok(await referenceData.GetSubjectByIdAsync(id, schoolId));
    }

    // DELETE /api/subjects/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteSubject(int id)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        if (!await referenceData.SubjectExistsAsync(id, schoolId))
        {
            return NotFound();
        }

        try
        {
            await referenceData.DeleteSubjectAsync(id);
        }
        catch (Npgsql.PostgresException ex)
        {
            logger.LogError(ex, "Failed to delete subject {Id}", id);
            return Conflict(new { message = "Cannot delete a subject that still has homework, assessments, or student enrollments referencing it." });
        }

        return NoContent();
    }
}

