using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Data.Stores;

namespace StudentConnect.Api.Controllers;

// Admin-only, read-only view over the append-only AuditLogs table.
[ApiController]
[Route("api/audit-logs")]
[Authorize(Roles = "Admin")]
public class AuditLogsController(AuditLogStore auditLogs) : ControllerBase
{
    // GET /api/audit-logs?entityType=Student&take=100
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AuditLogDto>>> GetRecent([FromQuery] string? entityType, [FromQuery] int take = 100)
    {
        var bounded = Math.Clamp(take, 1, 500);
        var logs = await auditLogs.GetRecentAsync(entityType, bounded);
        return Ok(logs.Select(AuditLogDto.FromEntity));
    }
}
