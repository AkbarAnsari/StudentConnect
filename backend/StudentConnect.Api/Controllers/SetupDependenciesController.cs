using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentConnect.Application;
using StudentConnect.Infrastructure.Data.Stores;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/setup-dependencies")]
[Authorize(Roles = "Admin")]
public class SetupDependenciesController(SetupDependencyStore dependencies, ICurrentTenantService currentTenant) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<SetupDependencyDto>>> GetAll()
    {
        if (currentTenant.SchoolId is not { } schoolId) return Forbid();
        return Ok(await dependencies.GetAllAsync(schoolId));
    }
}