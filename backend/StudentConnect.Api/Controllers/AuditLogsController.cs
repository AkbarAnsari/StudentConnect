using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Data.Stores;
using System.Text;

namespace StudentConnect.Api.Controllers;

// Admin-only, read-only view over the append-only AuditLogs table.
[ApiController]
[Route("api/audit-logs")]
[Authorize(Roles = "Admin")]
public class AuditLogsController(AuditLogStore auditLogs, ICurrentTenantService currentTenant) : ControllerBase
{
    [HttpGet("paged")]
    public async Task<ActionResult<PagedResult<AuditLogDto>>> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? entityType = null,
        [FromQuery] string? action = null,
        [FromQuery] string? username = null,
        [FromQuery] string? query = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null)
    {
        if (currentTenant.SchoolId is not { } schoolId) return Forbid();
        if (fromUtc > toUtc) return BadRequest(new { message = "From date must be earlier than To date." });
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 100);
        var result = await auditLogs.GetPagedAsync(schoolId, page, pageSize, entityType, action, username, query, fromUtc, toUtc?.AddDays(1));
        return Ok(new PagedResult<AuditLogDto> { Items = result.Items.Select(AuditLogDto.FromEntity).ToList(), TotalCount = result.TotalCount, Page = page, PageSize = pageSize });
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<AuditLogDto>> GetById(long id)
    {
        if (currentTenant.SchoolId is not { } schoolId) return Forbid();
        var item = await auditLogs.GetByIdAsync(id, schoolId);
        return item is null ? NotFound() : Ok(AuditLogDto.FromEntity(item));
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] string? entityType = null,
        [FromQuery] string? action = null,
        [FromQuery] string? username = null,
        [FromQuery] string? query = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null)
    {
        if (currentTenant.SchoolId is not { } schoolId) return Forbid();
        if (fromUtc > toUtc) return BadRequest(new { message = "From date must be earlier than To date." });
        var result = await auditLogs.GetPagedAsync(schoolId, 1, 5000, entityType, action, username, query, fromUtc, toUtc?.AddDays(1));
        var csv = new StringBuilder("Time UTC,Username,Role,Action,Entity Type,Entity ID,Details\r\n");
        foreach (var item in result.Items)
            csv.AppendLine($"{item.TimestampUtc:O},{Csv(item.Username)},{Csv(item.Role)},{Csv(item.Action)},{Csv(item.EntityType)},{Csv(item.EntityId)},{Csv(item.Details ?? string.Empty)}");
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"audit-log-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    // GET /api/audit-logs?entityType=Student&take=100
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AuditLogDto>>> GetRecent([FromQuery] string? entityType, [FromQuery] int take = 100)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        var bounded = Math.Clamp(take, 1, 500);
        var logs = await auditLogs.GetRecentAsync(schoolId, entityType, bounded);
        return Ok(logs.Select(AuditLogDto.FromEntity));
    }

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}
