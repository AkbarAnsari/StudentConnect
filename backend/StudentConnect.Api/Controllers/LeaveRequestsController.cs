using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Data.Stores;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/leave-requests")]
[Authorize]
public class LeaveRequestsController(
    LeaveRequestStore leaveRequests,
    StudentStore students,
    ICurrentUserService currentUser,
    ICurrentTenantService currentTenant,
    INotificationService notifications,
    IAuditService audit) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<LeaveRequestDto>>> Get(string? status = null)
    {
        if (currentTenant.SchoolId is not { } schoolId || currentUser.UserId is not { } userId) return Forbid();
        if (currentUser.Role == "Admin") return Ok(await leaveRequests.GetForSchoolAsync(schoolId, status));
        return Ok(await leaveRequests.GetForUserAsync(userId, schoolId));
    }

    [HttpPost]
    [Authorize(Roles = "Student,Parent")]
    public async Task<ActionResult<LeaveRequestDto>> Create(LeaveRequestCreateDto dto)
    {
        if (currentTenant.SchoolId is not { } schoolId || currentUser.UserId is not { } userId) return Forbid();
        if (!currentUser.CanAccessStudent(dto.StudentId) || !await students.ExistsAsync(dto.StudentId, schoolId)) return Forbid();
        if (dto.LeaveDate < DateOnly.FromDateTime(DateTime.UtcNow)) return BadRequest(new { message = "Leave date cannot be in the past." });
        if (string.IsNullOrWhiteSpace(dto.Reason)) return BadRequest(new { message = "Reason is required." });
        try
        {
            var id = await leaveRequests.CreateAsync(schoolId, dto.StudentId, userId, dto.LeaveDate, dto.Reason.Trim());
            await audit.LogAsync("Create", "LeaveRequest", id.ToString(), $"StudentId={dto.StudentId}; Date={dto.LeaveDate}");
            return CreatedAtAction(nameof(Get), null, await leaveRequests.GetByIdAsync(id, schoolId));
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Conflict(new { message = "A pending leave request already exists for this date." });
        }
    }

    [HttpPut("{id:int}/decision")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<LeaveRequestDto>> Decide(int id, LeaveRequestDecisionDto dto)
    {
        if (currentTenant.SchoolId is not { } schoolId || currentUser.UserId is not { } userId) return Forbid();
        var decision = string.Equals(dto.Decision, "Approved", StringComparison.OrdinalIgnoreCase) ? "Approved"
            : string.Equals(dto.Decision, "Rejected", StringComparison.OrdinalIgnoreCase) ? "Rejected" : null;
        if (decision is null) return BadRequest(new { message = "Decision must be Approved or Rejected." });
        var existing = await leaveRequests.GetByIdAsync(id, schoolId);
        if (existing is null) return NotFound();
        if (!await leaveRequests.DecideAsync(id, schoolId, decision, dto.Reason?.Trim(), userId)) return Conflict(new { message = "This request has already been decided." });
        await notifications.NotifyAsync(existing.RequestedByUserId, $"Leave request {decision.ToLowerInvariant()}", $"{existing.StudentName}: {existing.LeaveDate:yyyy-MM-dd}", NotificationType.General);
        await audit.LogAsync("Decision", "LeaveRequest", id.ToString(), $"{decision}; {dto.Reason}");
        return Ok(await leaveRequests.GetByIdAsync(id, schoolId));
    }
}