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
public class OpsController(Database db, EmailOutboxStore emailOutbox, ICurrentTenantService currentTenant, IWebHostEnvironment environment, OperationalSettingsStore? settingsStore = null, IHttpClientFactory? httpClientFactory = null) : ControllerBase
{
    private static readonly DateTime ProcessStartUtc = Process.GetCurrentProcess().StartTime.ToUniversalTime();

    // GET /api/ops/info
    [HttpGet("info")]
    public async Task<ActionResult<OpsInfoDto>> GetInfo()
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        var databaseHealthy = true;
        var databaseStopwatch = Stopwatch.StartNew();
        try
        {
            await db.ExecuteScalarAsync<int>("SELECT 1", bind: null);
        }
        catch
        {
            databaseHealthy = false;
        }
        databaseStopwatch.Stop();

        var delivery = await emailOutbox.GetDeliveryStatsAsync(schoolId);
        var settings = settingsStore is null ? new OperationalSettingsDto() : await settingsStore.GetAsync(schoolId);

        return Ok(new OpsInfoDto
        {
            EnvironmentName = environment.EnvironmentName,
            Version = typeof(OpsController).Assembly.GetName().Version?.ToString() ?? "unknown",
            ServerTimeUtc = DateTime.UtcNow,
            UptimeSeconds = (DateTime.UtcNow - ProcessStartUtc).TotalSeconds,
            DatabaseHealthy = databaseHealthy,
            DatabaseResponseMilliseconds = Math.Round(databaseStopwatch.Elapsed.TotalMilliseconds, 1),
            PendingEmailCount = delivery.Pending,
            FailedEmailCount = delivery.Failed,
            SentEmailCountLast24Hours = delivery.SentLast24Hours,
            OldestPendingEmailAtUtc = delivery.OldestPendingAtUtc,
            LastEmailSentAtUtc = delivery.LastSentAtUtc,
            LastEmailFailureAtUtc = delivery.LastFailureAtUtc,
            OverallStatus = !databaseHealthy || delivery.Failed > 0 ? "Attention" : delivery.Pending > 0 ? "Processing" : "Healthy",
            RunbookUrl = settings.RunbookUrl,
        });
    }

    [HttpGet("settings")]
    public async Task<ActionResult<OperationalSettingsDto>> GetSettings(){if(currentTenant.SchoolId is not{} schoolId||settingsStore is null)return Forbid();return Ok(await settingsStore.GetAsync(schoolId));}

    [HttpPut("settings")]
    public async Task<IActionResult> SaveSettings(OperationalSettingsDto dto){if(currentTenant.SchoolId is not{} schoolId||settingsStore is null)return Forbid();if(dto.FailedEmailThreshold<1||dto.AuditRetentionDays<30||dto.NotificationRetentionDays<30)return BadRequest(new{message="Threshold must be at least 1 and retention periods at least 30 days."});await settingsStore.SaveAsync(schoolId,dto);return NoContent();}

    [HttpPost("apply-retention")]
    public async Task<ActionResult<object>> ApplyRetention(){if(currentTenant.SchoolId is not{} schoolId||settingsStore is null)return Forbid();var settings=await settingsStore.GetAsync(schoolId);var result=await settingsStore.ApplyRetentionAsync(schoolId,settings);return Ok(new{auditRows=result.AuditRows,notificationRows=result.NotificationRows});}

    [HttpPost("test-alert")]
    public async Task<IActionResult> TestAlert(){if(currentTenant.SchoolId is not{} schoolId||settingsStore is null)return Forbid();var settings=await settingsStore.GetAsync(schoolId);const string message="StudentConnect operational alert destination test.";if(!string.IsNullOrWhiteSpace(settings.AlertEmail))await emailOutbox.EnqueueAsync(settings.AlertEmail,null,"StudentConnect alert test",message);if(!string.IsNullOrWhiteSpace(settings.AlertWebhookUrl)&&httpClientFactory is not null){var response=await httpClientFactory.CreateClient().PostAsJsonAsync(settings.AlertWebhookUrl,new{schoolId,status="Test",message,runbookUrl=settings.RunbookUrl});await settingsStore.LogAlertAsync(schoolId,settings.AlertWebhookUrl,message,response.IsSuccessStatusCode,response.IsSuccessStatusCode?null:response.StatusCode.ToString());}return Ok(new{message="Configured alert destinations were exercised."});}
}
