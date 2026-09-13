using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Data.Stores;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/academic-years")]
public class AcademicYearsController(AcademicYearStore academicYears, ICurrentTenantService currentTenant, ILogger<AcademicYearsController> logger) : ControllerBase
{
    // GET /api/academic-years
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IEnumerable<AcademicYearDto>>> GetAcademicYears()
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        var all = await academicYears.GetAllAsync(schoolId);
        return Ok(all.Select(AcademicYearDto.FromEntity));
    }

    // POST /api/academic-years
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AcademicYearDto>> CreateAcademicYear(AcademicYearCreateDto dto)
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

        int newId;
        try
        {
            newId = await academicYears.CreateAsync(new AcademicYear
            {
                Name = dto.Name,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                IsCurrent = dto.IsCurrent,
                SchoolId = schoolId,
            });
        }
        catch (Npgsql.PostgresException ex)
        {
            logger.LogError(ex, "Failed to save academic year {Name}", dto.Name);
            return StatusCode(500, new { message = "Unable to save the academic year. Please try again." });
        }

        var created = await academicYears.GetByIdAsync(newId, schoolId);
        return CreatedAtAction(nameof(GetAcademicYears), null, AcademicYearDto.FromEntity(created!));
    }

    // DELETE /api/academic-years/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteAcademicYear(int id)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        if (!await academicYears.ExistsAsync(id, schoolId))
        {
            return NotFound();
        }

        try
        {
            await academicYears.DeleteAsync(id);
        }
        catch (Npgsql.PostgresException ex)
        {
            logger.LogError(ex, "Failed to delete academic year {Id}", id);
            return Conflict(new { message = "Cannot delete an academic year that already has terms." });
        }

        return NoContent();
    }
}
