using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Data.Stores;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubjectsController(ReferenceDataStore referenceData, ILogger<SubjectsController> logger) : ControllerBase
{
    // GET /api/subjects — used by the (temporary, unauthenticated) Record Attendance page.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Subject>>> GetSubjects()
    {
        var subjects = await referenceData.GetAllSubjectsAsync();
        return Ok(subjects);
    }

    // POST /api/subjects
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Subject>> CreateSubject(SubjectCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Code))
        {
            return BadRequest(new { message = "Name and Code are required." });
        }

        if (await referenceData.SubjectCodeExistsAsync(dto.Code))
        {
            return Conflict(new { message = $"Subject code '{dto.Code}' already exists." });
        }

        var newId = await referenceData.CreateSubjectAsync(dto.Name, dto.Code);
        var created = await referenceData.GetSubjectByIdAsync(newId);
        return CreatedAtAction(nameof(GetSubjects), null, created);
    }

    // DELETE /api/subjects/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteSubject(int id)
    {
        if (!await referenceData.SubjectExistsAsync(id))
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

