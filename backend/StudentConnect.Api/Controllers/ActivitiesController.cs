using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Data.Stores;
using StudentConnect.Infrastructure.Notifications;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/activities")]
public class ActivitiesController(
    ActivityStore activities,
    ActivityRegistrationStore registrations,
    ReferenceDataStore referenceData,
    ICurrentTenantService currentTenant,
    ILogger<ActivitiesController> logger,
    ICurrentUserService? currentUser = null,
    StudentStore? students = null,
    NotificationRecipientResolver? recipients = null,
    INotificationService? notifications = null) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ActivityDto>>> GetActivities()
    {
        if (currentTenant.SchoolId is not { } schoolId) return Forbid();

        var all = await activities.GetAllAsync(schoolId);
        if (currentUser?.Role == "Teacher") all = all.Where(CanManage).ToList();
        if (currentUser?.Role is "Student" or "Parent")
        {
            var eligible = new List<Activity>();
            foreach (var activity in all.Where(item => item.Status == ActivityStatus.Open))
                if (await IsEligibleAsync(activity, schoolId)) eligible.Add(activity);
            all = eligible;
        }

        var result = new List<ActivityDto>();
        foreach (var activity in all)
        {
            result.Add(ActivityDto.FromEntity(
                activity,
                await registrations.CountActiveAsync(activity.Id),
                await registrations.CountWaitlistedAsync(activity.Id)));
        }
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ActivityDto>> GetActivityById(int id)
    {
        if (currentTenant.SchoolId is not { } schoolId) return Forbid();
        var activity = await activities.GetByIdAsync(id, schoolId);
        if (activity is null) return NotFound();
        if (currentUser?.Role == "Teacher" && !CanManage(activity)) return Forbid();
        if (currentUser?.Role is "Student" or "Parent" && (activity.Status != ActivityStatus.Open || !await IsEligibleAsync(activity, schoolId))) return NotFound();

        return Ok(ActivityDto.FromEntity(
            activity,
            await registrations.CountActiveAsync(id),
            await registrations.CountWaitlistedAsync(id)));
    }

    [HttpGet("grade/{gradeId:int}")]
    public async Task<ActionResult<IEnumerable<ActivityDto>>> GetActivitiesByGrade(int gradeId)
    {
        if (currentTenant.SchoolId is not { } schoolId || !await referenceData.GradeExistsAsync(gradeId, schoolId))
            return NotFound(new { message = "Grade not found." });

        var byGrade = await activities.GetByGradeAsync(gradeId, schoolId);
        if (currentUser?.Role == "Teacher") byGrade = byGrade.Where(CanManage).ToList();
        if (currentUser?.Role == "Student")
        {
            if (currentUser.StudentId is not { } studentId || students is null || await students.GetByIdAsync(studentId, schoolId) is not { } student || student.GradeId != gradeId)
                return NotFound(new { message = "Grade not found." });
            byGrade = byGrade.Where(activity => activity.SectionId is null || activity.SectionId == student.SectionId).ToList();
        }
        else if (currentUser?.Role == "Parent")
        {
            var eligible = new List<Activity>();
            foreach (var activity in byGrade.Where(item => item.Status == ActivityStatus.Open))
                if (await IsEligibleAsync(activity, schoolId)) eligible.Add(activity);
            byGrade = eligible;
        }

        var result = new List<ActivityDto>();
        foreach (var activity in byGrade)
        {
            result.Add(ActivityDto.FromEntity(
                activity,
                await registrations.CountActiveAsync(activity.Id),
                await registrations.CountWaitlistedAsync(activity.Id)));
        }
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Teacher,Admin,Coordinator")]
    public async Task<ActionResult<ActivityDto>> CreateActivity(ActivityCreateDto dto)
    {
        if (currentTenant.SchoolId is not { } schoolId) return Forbid();
        var validation = await ValidateAsync(dto.Title, dto.Location, dto.GradeId, dto.Capacity, schoolId);
        if (validation is not null) return validation;
        if (currentUser?.UserId is not { } ownerUserId) return Forbid();
        if (dto.SectionId is { } sectionId && !await referenceData.SectionBelongsToGradeAsync(sectionId, dto.GradeId, schoolId))
            return BadRequest(new { message = "Section must belong to the selected grade." });
        if (!TryValidateActivity(dto.Date, dto.RegistrationOpensAtUtc, dto.RegistrationClosesAtUtc, dto.Status, out var status, out var message))
            return BadRequest(new { message });
        if (dto.FeeAmount < 0) return BadRequest(new { message = "Fee amount cannot be negative." });
        var requiresModeration = currentUser.Role == "Teacher";
        if (requiresModeration) status = ActivityStatus.Draft;

        var activity = new Activity
        {
            Title = dto.Title.Trim(),
            Description = dto.Description.Trim(),
            Date = dto.Date,
            Location = dto.Location.Trim(),
            GradeId = dto.GradeId,
            SectionId = dto.SectionId,
            Capacity = dto.Capacity,
            CreatedDate = DateTime.UtcNow,
            OwnerUserId = ownerUserId,
            OrganizerName = string.IsNullOrWhiteSpace(dto.OrganizerName) ? currentUser.Username ?? "School activity" : dto.OrganizerName.Trim(),
            ContactEmail = string.IsNullOrWhiteSpace(dto.ContactEmail) ? null : dto.ContactEmail.Trim(),
            RegistrationOpensAtUtc = dto.RegistrationOpensAtUtc,
            RegistrationClosesAtUtc = dto.RegistrationClosesAtUtc,
            ConsentRequired = dto.ConsentRequired,
            FeeAmount = dto.FeeAmount is > 0 ? dto.FeeAmount : null,
            ModerationStatus = requiresModeration ? "Pending" : "Approved",
            Status = status,
        };

        try
        {
            activity.Id = await activities.CreateAsync(activity);
        }
        catch (PostgresException ex)
        {
            logger.LogError(ex, "Failed to save activity {Title}", dto.Title);
            return StatusCode(500, new { message = "Unable to save the activity. Please try again." });
        }

        var created = await activities.GetByIdAsync(activity.Id, schoolId);
        return CreatedAtAction(nameof(GetActivityById), new { id = activity.Id }, ActivityDto.FromEntity(created!, 0));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Teacher,Admin,Coordinator")]
    public async Task<ActionResult<ActivityDto>> UpdateActivity(int id, ActivityUpdateDto dto)
    {
        if (currentTenant.SchoolId is not { } schoolId) return Forbid();
        if (string.IsNullOrWhiteSpace(dto.Title) || string.IsNullOrWhiteSpace(dto.Location))
            return BadRequest(new { message = "Title and Location are required." });
        if (dto.Capacity <= 0) return BadRequest(new { message = "Capacity must be greater than zero." });
        if (dto.FeeAmount < 0) return BadRequest(new { message = "Fee amount cannot be negative." });

        var activity = await activities.GetByIdAsync(id, schoolId);
        if (activity is null) return NotFound();
        if (!CanManage(activity)) return Forbid();
        if (activity.Status is ActivityStatus.Cancelled or ActivityStatus.Completed or ActivityStatus.Archived)
            return Conflict(new { message = "Cancelled, completed, and archived activities cannot be edited." });
        if (dto.SectionId is { } sectionId && !await referenceData.SectionBelongsToGradeAsync(sectionId, activity.GradeId, schoolId))
            return BadRequest(new { message = "Section must belong to the activity grade." });
        if (!TryValidateWindow(dto.Date, dto.RegistrationOpensAtUtc, dto.RegistrationClosesAtUtc, out var message))
            return BadRequest(new { message });

        var activeCount = await registrations.CountActiveAsync(id);
        if (dto.Capacity < activeCount)
            return BadRequest(new { message = $"Capacity cannot be less than the current number of active registrations ({activeCount})." });

        try
        {
            await activities.UpdateAsync(
                id, dto.Title.Trim(), dto.Description.Trim(), dto.Date, dto.Location.Trim(), dto.Capacity,
                dto.SectionId, dto.OrganizerName.Trim(), string.IsNullOrWhiteSpace(dto.ContactEmail) ? null : dto.ContactEmail.Trim(),
                dto.RegistrationOpensAtUtc, dto.RegistrationClosesAtUtc, dto.ConsentRequired, dto.FeeAmount is > 0 ? dto.FeeAmount : null);
        }
        catch (PostgresException ex)
        {
            logger.LogError(ex, "Failed to update activity {Id}", id);
            return StatusCode(500, new { message = "Unable to update the activity. Please try again." });
        }

        var updated = await activities.GetByIdAsync(id, schoolId);
        return Ok(ActivityDto.FromEntity(updated!, activeCount, await registrations.CountWaitlistedAsync(id)));
    }

    [HttpPut("{id:int}/status")]
    [Authorize(Roles = "Teacher,Admin,Coordinator")]
    public async Task<ActionResult<ActivityDto>> UpdateStatus(int id, ActivityStatusUpdateDto dto)
    {
        if (currentTenant.SchoolId is not { } schoolId) return Forbid();
        var activity = await activities.GetByIdAsync(id, schoolId);
        if (activity is null) return NotFound();
        if (!CanManage(activity)) return Forbid();
        if (!Enum.TryParse<ActivityStatus>(dto.Status, true, out var target) || !CanTransition(activity.Status, target))
            return BadRequest(new { message = $"Cannot move an activity from {activity.Status} to {dto.Status}." });
        if (target == ActivityStatus.Open && activity.ModerationStatus != "Approved")
            return Conflict(new { message = "This activity must be approved before registration can open." });
        if (target == ActivityStatus.Completed && activity.Date > DateTime.UtcNow)
            return BadRequest(new { message = "An activity cannot be completed before its scheduled time." });
        if (target == ActivityStatus.Open && activity.Date <= DateTime.UtcNow)
            return BadRequest(new { message = "A past activity cannot be opened for registration." });

        await activities.SetStatusAsync(id, target);

        if (target == ActivityStatus.Cancelled && recipients is not null && notifications is not null)
        {
            var studentIds = (await registrations.GetByActivityAsync(id))
                .Where(item => item.Status is ActivityRegistrationStatus.Registered or ActivityRegistrationStatus.Waitlisted)
                .Select(item => item.StudentId)
                .Distinct()
                .ToList();
            var userIds = await recipients.ForStudentsAsync(studentIds);
            if (userIds.Count > 0)
                await notifications.NotifyManyTrackedAsync(userIds, $"{activity.Title} cancelled", "This activity has been cancelled by its organizer.", NotificationType.General, "/notifications", "Important", null);
        }

        var updated = await activities.GetByIdAsync(id, schoolId);
        return Ok(ActivityDto.FromEntity(
            updated!,
            await registrations.CountActiveAsync(id),
            await registrations.CountWaitlistedAsync(id)));
    }

    [HttpPut("{id:int}/moderation")]
    [Authorize(Roles = "Teacher,Admin,Coordinator")]
    public async Task<ActionResult<ActivityDto>> Moderate(int id, ActivityModerationDto dto)
    {
        if (currentTenant.SchoolId is not { } schoolId || currentUser?.UserId is not { } userId) return Forbid();
        var activity = await activities.GetByIdAsync(id, schoolId);
        if (activity is null) return NotFound();
        var action = dto.Action?.Trim();
        if (string.Equals(action, "Submit", StringComparison.OrdinalIgnoreCase))
        {
            if (!CanManage(activity) || currentUser.Role != "Teacher") return Forbid();
            await activities.SetModerationAsync(id, "Pending", dto.Reason?.Trim(), userId);
        }
        else if (string.Equals(action, "Approve", StringComparison.OrdinalIgnoreCase) || string.Equals(action, "Reject", StringComparison.OrdinalIgnoreCase))
        {
            if (currentUser.Role is not ("Admin" or "Coordinator")) return Forbid();
            var target = string.Equals(action, "Approve", StringComparison.OrdinalIgnoreCase) ? "Approved" : "Rejected";
            if (target == "Rejected" && string.IsNullOrWhiteSpace(dto.Reason)) return BadRequest(new { message = "A rejection reason is required." });
            await activities.SetModerationAsync(id, target, dto.Reason?.Trim(), userId);
        }
        else return BadRequest(new { message = "Action must be Submit, Approve, or Reject." });
        return Ok(ActivityDto.FromEntity((await activities.GetByIdAsync(id, schoolId))!, await registrations.CountActiveAsync(id), await registrations.CountWaitlistedAsync(id)));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Teacher,Admin,Coordinator")]
    public async Task<IActionResult> DeleteActivity(int id)
    {
        if (currentTenant.SchoolId is not { } schoolId) return Forbid();
        var activity = await activities.GetByIdAsync(id, schoolId);
        if (activity is null) return NotFound();
        if (!CanManage(activity)) return Forbid();
        if (activity.Status != ActivityStatus.Draft || await registrations.ExistsAnyForActivityAsync(id))
            return Conflict(new { message = "Only draft activities without registrations can be deleted. Cancel or archive this activity instead." });

        try
        {
            await activities.DeleteAsync(id);
        }
        catch (PostgresException ex)
        {
            logger.LogError(ex, "Failed to delete activity {Id}", id);
            return StatusCode(500, new { message = "Unable to delete the activity. Please try again." });
        }
        return NoContent();
    }

    private bool CanManage(Activity activity) => currentUser?.Role is "Admin" or "Coordinator" || activity.OwnerUserId == currentUser?.UserId;

    private async Task<ActionResult?> ValidateAsync(string title, string location, int gradeId, int capacity, int schoolId)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(location))
            return BadRequest(new { message = "Title and Location are required." });
        if (capacity <= 0) return BadRequest(new { message = "Capacity must be greater than zero." });
        if (!await referenceData.GradeExistsAsync(gradeId, schoolId))
            return BadRequest(new { message = "Grade does not exist." });
        return null;
    }

    private static bool TryValidateActivity(DateTime date, DateTime? opensAt, DateTime? closesAt, string requestedStatus, out ActivityStatus status, out string message)
    {
        if (!Enum.TryParse(requestedStatus, true, out status) || status is not (ActivityStatus.Draft or ActivityStatus.Open))
        {
            message = "New activities must be Draft or Open.";
            return false;
        }
        if (status == ActivityStatus.Open && date <= DateTime.UtcNow)
        {
            message = "A past activity cannot be opened for registration.";
            return false;
        }
        return TryValidateWindow(date, opensAt, closesAt, out message);
    }

    private static bool TryValidateWindow(DateTime date, DateTime? opensAt, DateTime? closesAt, out string message)
    {
        if (opensAt is not null && closesAt is not null && opensAt >= closesAt)
        {
            message = "Registration closing time must be later than its opening time.";
            return false;
        }
        if (closesAt is not null && closesAt >= date)
        {
            message = "Registration must close before the activity begins.";
            return false;
        }
        message = string.Empty;
        return true;
    }

    private static bool CanTransition(ActivityStatus current, ActivityStatus target) => current switch
    {
        ActivityStatus.Draft => target is ActivityStatus.Open or ActivityStatus.Cancelled,
        ActivityStatus.Open => target is ActivityStatus.Closed or ActivityStatus.Cancelled or ActivityStatus.Completed,
        ActivityStatus.Closed => target is ActivityStatus.Open or ActivityStatus.Cancelled or ActivityStatus.Completed,
        ActivityStatus.Cancelled or ActivityStatus.Completed => target == ActivityStatus.Archived,
        _ => false,
    };

    private async Task<bool> IsEligibleAsync(Activity activity, int schoolId)
    {
        if (students is null || currentUser is null) return false;
        var studentIds = currentUser.StudentId is { } studentId ? new[] { studentId } : currentUser.ChildStudentIds;
        foreach (var id in studentIds)
        {
            if (await students.GetByIdAsync(id, schoolId) is { } student && student.GradeId == activity.GradeId &&
                (activity.SectionId is null || student.SectionId == activity.SectionId)) return true;
        }
        return false;
    }
}