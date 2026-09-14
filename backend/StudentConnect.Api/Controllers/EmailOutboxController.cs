using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Data.Stores;

namespace StudentConnect.Api.Controllers;

// Admin-only, read-only view over the EmailOutbox table — lets an admin see that the
// async outbox/processor pipeline is actually delivering (or retrying/failing) email.
[ApiController]
[Route("api/email-outbox")]
[Authorize(Roles = "Admin")]
public class EmailOutboxController(EmailOutboxStore outbox, IAuditService audit, ICurrentTenantService currentTenant) : ControllerBase
{
    // GET /api/email-outbox?take=50
    [HttpGet]
    public async Task<ActionResult<IEnumerable<EmailOutboxDto>>> GetRecent([FromQuery] int take = 50)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        var bounded = Math.Clamp(take, 1, 200);
        var entries = await outbox.GetRecentAsync(schoolId, bounded);
        return Ok(entries.Select(EmailOutboxDto.FromEntity));
    }

    // POST /api/email-outbox/{id}/retry — resets the attempt counter so the background
    // processor picks a failed entry back up on its next poll.
    [HttpPost("{id:int}/retry")]
    public async Task<IActionResult> Retry(int id)
    {
        if (currentTenant.SchoolId is not { } schoolId || !await outbox.ExistsAsync(id, schoolId))
        {
            return NotFound();
        }

        await outbox.RetryAsync(id);
        await audit.LogAsync("Retry", "EmailOutbox", id.ToString());
        return NoContent();
    }
}
