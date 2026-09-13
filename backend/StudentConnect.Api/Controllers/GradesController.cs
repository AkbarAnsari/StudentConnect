using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentConnect.Api.Data.Stores;
using StudentConnect.Api.Models;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GradesController(ReferenceDataStore referenceData, ILogger<GradesController> logger) : ControllerBase
{
    // GET /api/grades — used by Angular's Add/Edit Student forms to populate the grade dropdown.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Grade>>> GetGrades()
    {
        var grades = await referenceData.GetAllGradesAsync();
        return Ok(grades);
    }

    // POST /api/grades
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Grade>> CreateGrade(GradeCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return BadRequest(new { message = "Name is required." });
        }

        if (await referenceData.GradeNameExistsAsync(dto.Name))
        {
            return Conflict(new { message = $"Grade '{dto.Name}' already exists." });
        }

        var newId = await referenceData.CreateGradeAsync(dto.Name);
        var created = await referenceData.GetGradeByIdAsync(newId);
        return CreatedAtAction(nameof(GetGrades), null, created);
    }

    // DELETE /api/grades/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteGrade(int id)
    {
        if (!await referenceData.GradeExistsAsync(id))
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

