using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Data.Stores;

namespace StudentConnect.Api.Controllers;

// Cross-entity search for staff — Students/Teachers/Homework/LearningMaterials/Activities.
// Teacher/Admin only: a Student- or Parent-scoped search would need per-row access checks
// this endpoint doesn't do, so it's kept to the roles that can already see everything.
[ApiController]
[Route("api/search")]
[Authorize(Roles = "Teacher,Admin")]
public class SearchController(SearchStore search, ICurrentTenantService currentTenant) : ControllerBase
{
    // GET /api/search?q=...&take=20
    [HttpGet]
    public async Task<ActionResult<IEnumerable<SearchResultDto>>> Search([FromQuery] string q, [FromQuery] int take = 20)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(q))
        {
            return Ok(Array.Empty<SearchResultDto>());
        }

        var bounded = Math.Clamp(take, 1, 100);
        var results = await search.SearchAsync(q, bounded, schoolId);
        return Ok(results);
    }
}
