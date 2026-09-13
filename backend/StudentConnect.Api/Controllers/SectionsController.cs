using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentConnect.Api.Data.Stores;
using StudentConnect.Api.Models;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/sections")]
public class SectionsController(SectionStore sections, ReferenceDataStore referenceData, ILogger<SectionsController> logger) : ControllerBase
{
    // GET /api/sections
    [HttpGet]
    public async Task<ActionResult<IEnumerable<SectionDto>>> GetSections()
    {
        var all = await sections.GetAllAsync();
        return Ok(all.Select(SectionDto.FromEntity));
    }

    // GET /api/sections/grade/{gradeId} — used by the Add/Edit Student forms' Section dropdown.
    [HttpGet("grade/{gradeId:int}")]
    public async Task<ActionResult<IEnumerable<SectionDto>>> GetByGrade(int gradeId)
    {
        if (!await referenceData.GradeExistsAsync(gradeId))
        {
            return NotFound(new { message = "Grade not found." });
        }

        var byGrade = await sections.GetByGradeAsync(gradeId);
        return Ok(byGrade.Select(SectionDto.FromEntity));
    }

    // POST /api/sections
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<SectionDto>> CreateSection(SectionCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return BadRequest(new { message = "Name is required." });
        }

        if (!await referenceData.GradeExistsAsync(dto.GradeId))
        {
            return BadRequest(new { message = "Grade does not exist." });
        }

        if (await sections.NameExistsInGradeAsync(dto.GradeId, dto.Name))
        {
            return Conflict(new { message = $"Section '{dto.Name}' already exists for this grade." });
        }

        int newId;
        try
        {
            newId = await sections.CreateAsync(new Section { GradeId = dto.GradeId, Name = dto.Name });
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

    // DELETE /api/sections/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteSection(int id)
    {
        if (!await sections.ExistsAsync(id))
        {
            return NotFound();
        }

        try
        {
            await sections.DeleteAsync(id);
        }
        catch (Npgsql.PostgresException ex)
        {
            logger.LogError(ex, "Failed to delete section {Id}", id);
            return Conflict(new { message = "Cannot delete a section that still has students assigned to it." });
        }

        return NoContent();
    }
}
