using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentConnect.Api.Data.Stores;
using StudentConnect.Api.Models;

namespace StudentConnect.Api.Controllers;

// Admin-only, read-only view over the EmailOutbox table — lets an admin see that the
// async outbox/processor pipeline is actually delivering (or retrying/failing) email.
[ApiController]
[Route("api/email-outbox")]
[Authorize(Roles = "Admin")]
public class EmailOutboxController(EmailOutboxStore outbox) : ControllerBase
{
    // GET /api/email-outbox?take=50
    [HttpGet]
    public async Task<ActionResult<IEnumerable<EmailOutboxDto>>> GetRecent([FromQuery] int take = 50)
    {
        var bounded = Math.Clamp(take, 1, 200);
        var entries = await outbox.GetRecentAsync(bounded);
        return Ok(entries.Select(EmailOutboxDto.FromEntity));
    }
}
