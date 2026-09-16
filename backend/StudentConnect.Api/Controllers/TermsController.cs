using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Data.Stores;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/terms")]
public class TermsController(TermStore terms, AcademicYearStore academicYears, ICurrentTenantService currentTenant, ILogger<TermsController> logger) : ControllerBase
{
    // GET /api/terms
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IEnumerable<TermDto>>> GetTerms()
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        var all = await terms.GetAllAsync(schoolId);
        return Ok(all.Select(TermDto.FromEntity));
    }

    // GET /api/terms/academic-year/{academicYearId}
    [HttpGet("academic-year/{academicYearId:int}")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<TermDto>>> GetByAcademicYear(int academicYearId)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        if (!await academicYears.ExistsAsync(academicYearId, schoolId))
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
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return BadRequest(new { message = "Name is required." });
        }

        if (dto.EndDate <= dto.StartDate)
        {
            return BadRequest(new { message = "EndDate must be after StartDate." });
        }

        if (!await academicYears.ExistsAsync(dto.AcademicYearId, schoolId))
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

        var all = await terms.GetAllAsync(schoolId);
        var created = all.First(t => t.Id == newId);
        return CreatedAtAction(nameof(GetTerms), null, TermDto.FromEntity(created));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<TermDto>> UpdateTerm(int id, TermUpdateDto dto)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        var term = await terms.GetByIdAsync(id, schoolId);
        if (term is null)
        {
            return NotFound();
        }

        if (term.IsLocked)
        {
            return Conflict(new { message = "Unlock this term before editing it." });
        }

        var name = (dto.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new { message = "Name is required." });
        }

        if (dto.EndDate <= dto.StartDate)
        {
            return BadRequest(new { message = "EndDate must be after StartDate." });
        }

        await terms.UpdateAsync(id, name, dto.StartDate, dto.EndDate);
        return Ok(TermDto.FromEntity((await terms.GetByIdAsync(id, schoolId))!));
    }

    // DELETE /api/terms/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteTerm(int id)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        if (!await terms.ExistsAsync(id, schoolId))
        {
            return NotFound();
        }

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

    // POST /api/terms/{id}/lock — once locked, no Assessment/Mark tied to this term can be
    // created, edited, or deleted (see AssessmentsController/MarksController).
    [HttpPost("{id:int}/lock")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> LockTerm(int id)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        if (!await terms.ExistsAsync(id, schoolId))
        {
            return NotFound();
        }

        await terms.LockAsync(id);
        return NoContent();
    }

    // POST /api/terms/{id}/unlock
    [HttpPost("{id:int}/unlock")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UnlockTerm(int id)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        if (!await terms.ExistsAsync(id, schoolId))
        {
            return NotFound();
        }

        await terms.UnlockAsync(id);
        return NoContent();
    }
}
