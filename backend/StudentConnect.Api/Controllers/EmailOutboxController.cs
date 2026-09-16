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
public class EmailOutboxController(EmailOutboxStore outbox, IAuditService audit, ICurrentTenantService currentTenant, ICurrentUserService? currentUser = null) : ControllerBase
{
    [HttpGet("paged")]
    public async Task<ActionResult<PagedResult<EmailOutboxDto>>> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? status = null,
        [FromQuery] string? query = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null)
    {
        if (currentTenant.SchoolId is not { } schoolId) return Forbid();
        if (status is not null && status.ToLowerInvariant() is not ("sent" or "failed" or "pending"))
            return BadRequest(new { message = "Status must be sent, failed, or pending." });
        if (fromUtc > toUtc) return BadRequest(new { message = "From date must be earlier than To date." });
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 100);
        var result = await outbox.GetPagedAsync(schoolId, page, pageSize, status, query, fromUtc, toUtc?.AddDays(1));
        return Ok(new PagedResult<EmailOutboxDto> { Items = result.Items.Select(EmailOutboxDto.FromEntity).ToList(), TotalCount = result.TotalCount, Page = page, PageSize = pageSize });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EmailOutboxDto>> GetById(int id)
    {
        if (currentTenant.SchoolId is not { } schoolId) return Forbid();
        var item = await outbox.GetByIdAsync(id, schoolId);
        return item is null ? NotFound() : Ok(EmailOutboxDto.FromEntity(item));
    }

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
        if (currentTenant.SchoolId is not { } schoolId) return Forbid();
        if (await outbox.RetryFailedAsync(id, schoolId) == 0)
            return Conflict(new { message = "Only failed, unsent emails can be retried." });
        await audit.LogAsync("Retry", "EmailOutbox", id.ToString());
        return NoContent();
    }

    [HttpPost("retry-failed")]
    public async Task<ActionResult<object>> RetryFailed(EmailOutboxRetryRequestDto request)
    {
        if (currentTenant.SchoolId is not { } schoolId) return Forbid();
        var ids = request.Ids.Distinct().Take(100).ToArray();
        if (ids.Length == 0) return BadRequest(new { message = "Select at least one failed email." });
        var retried = await outbox.RetryFailedAsync(ids, schoolId);
        await audit.LogAsync("BulkRetry", "EmailOutbox", string.Join(',', ids), $"Requested={ids.Length}, Retried={retried}");
        return Ok(new { requested = ids.Length, retried });
    }

    [HttpPost("{id:int}/assign-to-me")]
    public async Task<IActionResult> AssignToMe(int id,DeadLetterAssignmentDto dto)
    {
        if(currentTenant.SchoolId is not{} schoolId||currentUser?.UserId is not{} userId)return Forbid();
        if(await outbox.AssignDeadLetterAsync(id,schoolId,userId,dto.ResolutionNotes?.Trim())==0)return Conflict(new{message="Only failed unsent emails can be assigned."});
        await audit.LogAsync("Assign","EmailOutbox",id.ToString(),dto.ResolutionNotes);return NoContent();
    }
}
