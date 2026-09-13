using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentConnect.Api.Data.Stores;
using StudentConnect.Api.Models;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/terms")]
public class TermsController(TermStore terms, AcademicYearStore academicYears, ILogger<TermsController> logger) : ControllerBase
{
    // GET /api/terms
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TermDto>>> GetTerms()
    {
        var all = await terms.GetAllAsync();
        return Ok(all.Select(TermDto.FromEntity));
    }

    // GET /api/terms/academic-year/{academicYearId}
    [HttpGet("academic-year/{academicYearId:int}")]
    public async Task<ActionResult<IEnumerable<TermDto>>> GetByAcademicYear(int academicYearId)
    {
        if (!await academicYears.ExistsAsync(academicYearId))
        {
            return NotFound(new { message = "Academic year not found." });
        }

        var byYear = await terms.GetByAcademicYearAsync(academicYearId);
        return Ok(byYear.Select(TermDto.FromEntity));
    }

    // POST /api/terms
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<TermDto>> CreateTerm(TermCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return BadRequest(new { message = "Name is required." });
        }

        if (dto.EndDate <= dto.StartDate)
        {
            return BadRequest(new { message = "EndDate must be after StartDate." });
        }

        if (!await academicYears.ExistsAsync(dto.AcademicYearId))
        {
            return BadRequest(new { message = "Academic year does not exist." });
        }

        int newId;
        try
        {
            newId = await terms.CreateAsync(new Term
            {
                AcademicYearId = dto.AcademicYearId,
                Name = dto.Name,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
            });
        }
        catch (Npgsql.PostgresException ex)
        {
            logger.LogError(ex, "Failed to save term {Name}", dto.Name);
            return StatusCode(500, new { message = "Unable to save the term. Please try again." });
        }

        var all = await terms.GetAllAsync();
        var created = all.First(t => t.Id == newId);
        return CreatedAtAction(nameof(GetTerms), null, TermDto.FromEntity(created));
    }

    // DELETE /api/terms/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteTerm(int id)
    {
        try
        {
            await terms.DeleteAsync(id);
        }
        catch (Npgsql.PostgresException ex)
        {
            logger.LogError(ex, "Failed to delete term {Id}", id);
            return Conflict(new { message = "Unable to delete this term." });
        }

        return NoContent();
    }
}
