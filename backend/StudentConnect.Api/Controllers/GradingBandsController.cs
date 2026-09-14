using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentConnect.Application;
using StudentConnect.Infrastructure.Data.Stores;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/grading-bands")]
public class GradingBandsController(GradingBandStore gradingBands, ICurrentTenantService currentTenant, IAuditService audit) : ControllerBase
{
    // GET /api/grading-bands
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IEnumerable<GradingBandDto>>> GetBands()
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        var bands = await gradingBands.GetForSchoolAsync(schoolId);
        return Ok(bands.Select(b => new GradingBandDto { Id = b.Id, Label = b.Label, MinPercentage = b.MinPercentage }));
    }

    // PUT /api/grading-bands — replaces the whole set for the caller's school.
    [HttpPut]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<GradingBandDto>>> ReplaceBands(GradingBandReplaceDto dto)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        if (dto.Bands.Count == 0)
        {
            return BadRequest(new { message = "At least one band is required." });
        }

        if (dto.Bands.Any(b => string.IsNullOrWhiteSpace(b.Label)))
        {
            return BadRequest(new { message = "Every band needs a label." });
        }

        await gradingBands.ReplaceAllAsync(schoolId, dto.Bands.Select(b => (b.Label, b.MinPercentage)).ToList());
        await audit.LogAsync("Update", "GradingBands", schoolId.ToString(), $"{dto.Bands.Count} band(s)");

        var updated = await gradingBands.GetForSchoolAsync(schoolId);
        return Ok(updated.Select(b => new GradingBandDto { Id = b.Id, Label = b.Label, MinPercentage = b.MinPercentage }));
    }
}
