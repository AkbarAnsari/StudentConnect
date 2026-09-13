using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Data.Stores;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GradesController(ReferenceDataStore referenceData, ICurrentTenantService currentTenant, ILogger<GradesController> logger) : ControllerBase
{
    // GET /api/grades — used by Angular's Add/Edit Student forms to populate the grade dropdown,
    // and (anonymously — see SubjectsController) by the temporary unauthenticated Record
    // Attendance page. Tenant-scoped when the caller is authenticated; falls back to the
    // pre-existing unscoped list for the anonymous case, unchanged from before multi-tenancy.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Grade>>> GetGrades()
    {
        var grades = currentTenant.SchoolId is { } schoolId
            ? await referenceData.GetAllGradesAsync(schoolId)
            : await referenceData.GetAllGradesAsync();
        return Ok(grades);
    }

    // POST /api/grades
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Grade>> CreateGrade(GradeCreateDto dto)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return BadRequest(new { message = "Name is required." });
        }

        if (await referenceData.GradeNameExistsAsync(dto.Name, schoolId))
        {
            return Conflict(new { message = $"Grade '{dto.Name}' already exists." });
        }

        var newId = await referenceData.CreateGradeAsync(dto.Name, schoolId);
        var created = await referenceData.GetGradeByIdAsync(newId, schoolId);
        return CreatedAtAction(nameof(GetGrades), null, created);
    }

    // DELETE /api/grades/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteGrade(int id)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        if (!await referenceData.GradeExistsAsync(id, schoolId))
        {
            return NotFound();
        }

        try
        {
            await referenceData.DeleteGradeAsync(id);
        }
        catch (Npgsql.PostgresException ex)
        {
            logger.LogError(ex, "Failed to delete grade {Id}", id);
            return Conflict(new { message = "Cannot delete a grade that still has students or sections assigned to it." });
        }

        return NoContent();
    }
}

