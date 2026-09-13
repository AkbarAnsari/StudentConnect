using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Data;
using StudentConnect.Infrastructure.Data.Stores;

namespace StudentConnect.Api.Controllers;

// Admin-only operational snapshot — a richer, authenticated companion to the plain
// unauthenticated /health probe (see Program.cs), meant for a human looking at the ops page.
[ApiController]
[Route("api/ops")]
[Authorize(Roles = "Admin")]
public class OpsController(Database db, EmailOutboxStore emailOutbox, IWebHostEnvironment environment) : ControllerBase
{
    private static readonly DateTime ProcessStartUtc = Process.GetCurrentProcess().StartTime.ToUniversalTime();

    // GET /api/ops/info
    [HttpGet("info")]
    public async Task<ActionResult<OpsInfoDto>> GetInfo()
    {
        var databaseHealthy = true;
        try
        {
            await db.ExecuteScalarAsync<int>("SELECT 1", bind: null);
        }
        catch
        {
            databaseHealthy = false;
        }

        var (pending, failed) = await emailOutbox.GetStatsAsync();

        return Ok(new OpsInfoDto
        {
            EnvironmentName = environment.EnvironmentName,
            Version = typeof(OpsController).Assembly.GetName().Version?.ToString() ?? "unknown",
            ServerTimeUtc = DateTime.UtcNow,
            UptimeSeconds = (DateTime.UtcNow - ProcessStartUtc).TotalSeconds,
            DatabaseHealthy = databaseHealthy,
            PendingEmailCount = pending,
            FailedEmailCount = failed,
        });
    }
}
