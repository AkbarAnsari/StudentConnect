using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Data.Stores;
using StudentConnect.Infrastructure.Notifications;
using StudentConnect.Infrastructure.Storage;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/announcements")]
[Authorize(Roles = "Teacher,Admin")]
public class AnnouncementsController(
    AnnouncementStore announcements,
    StudentStore students,
    ReferenceDataStore referenceData,
    SectionStore sections,
    TeacherSubjectAssignmentStore assignments,
    NotificationRecipientResolver recipients,
    INotificationService notifications,
    ICurrentUserService currentUser,
    ICurrentTenantService currentTenant,
    IAuditService audit,
    ILogger<AnnouncementsController> logger,
    IFileStorage? fileStorage = null,
    IOptions<FileStorageOptions>? storageOptions = null) : ControllerBase
{
    // GET /api/announcements?take=20
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AnnouncementDto>>> GetRecent(int take = 20)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        await announcements.ExpireDueAsync();
        return Ok(await announcements.GetRecentAsync(schoolId, Math.Clamp(take, 1, 100)));
    }

    [HttpPost("preview-audience")]
    public async Task<ActionResult<AnnouncementAudienceResultDto>> PreviewAudience(AnnouncementAudiencePreviewDto dto)
    {
        if (currentTenant.SchoolId is not { } schoolId) return Forbid();
        var result = await ResolveAudienceAsync(dto.ScopeType, dto.ScopeGradeId, dto.ScopeSectionId, schoolId);
        return result.Error is not null
            ? BadRequest(new { message = result.Error })
            : Ok(new AnnouncementAudienceResultDto { StudentCount = result.StudentIds.Count, RecipientCount = result.RecipientUserIds.Count });
    }

    // POST /api/announcements — Teacher may target any Grade/Section/the whole School within
    // their own School, matching this codebase's existing, deliberately permissive Teacher-trust
    // model (any Teacher can already create Homework for any Grade in their school).
    [HttpPost]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<AnnouncementDto>> CreateAnnouncement([FromForm] AnnouncementCreateDto dto)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        if (currentUser.UserId is not { } authorUserId)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(dto.Title) || string.IsNullOrWhiteSpace(dto.Body))
        {
            return BadRequest(new { message = "Title and Body are required." });
        }

        if (!Enum.TryParse<AnnouncementScope>(dto.ScopeType, true, out var scope))
        {
            return BadRequest(new { message = "ScopeType must be one of School, Grade, Section." });
        }

        if (!Enum.TryParse<AnnouncementStatus>(dto.Status, true, out var status) || status is AnnouncementStatus.Expired or AnnouncementStatus.Withdrawn)
        {
            return BadRequest(new { message = "Status must be Draft, Scheduled, or Sent." });
        }

        if (!Enum.TryParse<AnnouncementPriority>(dto.Priority, true, out var priority))
        {
            return BadRequest(new { message = "Priority must be Normal, Important, or Urgent." });
        }

        if (status == AnnouncementStatus.Scheduled && (dto.ScheduledForUtc is null || dto.ScheduledForUtc <= DateTime.UtcNow))
        {
            return BadRequest(new { message = "A future scheduled time is required." });
        }

        if (dto.ExpiresAtUtc is not null && dto.ExpiresAtUtc <= (dto.ScheduledForUtc ?? DateTime.UtcNow))
        {
            return BadRequest(new { message = "Expiry must be later than delivery." });
        }

        if (currentUser.Role == "Teacher")
        {
            if (currentUser.TeacherId is not { } teacherId ||
                scope != AnnouncementScope.Section ||
                dto.ScopeSectionId is not { } teacherSectionId ||
                !await assignments.TeacherHasSectionAsync(teacherId, teacherSectionId, schoolId))
            {
                return Forbid();
            }
        }

        var audience = await ResolveAudienceAsync(scope.ToString(), dto.ScopeGradeId, dto.ScopeSectionId, schoolId);
        if (audience.Error is not null) return BadRequest(new { message = audience.Error });
        if (status != AnnouncementStatus.Draft && audience.RecipientUserIds.Count == 0)
            return BadRequest(new { message = "This audience has no active user recipients." });

        string? attachmentStoredFileName = null;
        if (dto.Attachment is { Length: > 0 } attachment)
        {
            if (fileStorage is null || storageOptions is null) return StatusCode(500, new { message = "Attachment storage is unavailable." });
            if (attachment.Length > storageOptions.Value.MaxFileSizeBytes) return BadRequest(new { message = "Attachment exceeds the maximum file size." });
            var extension = Path.GetExtension(attachment.FileName);
            if (!AllowedFileTypes.IsExtensionAllowed(extension)) return BadRequest(new { message = "This attachment type is not supported." });
            await using (var check = attachment.OpenReadStream()) if (!await AllowedFileTypes.MatchesSignatureAsync(extension, check)) return BadRequest(new { message = "This attachment type is not supported." });
            attachmentStoredFileName = $"announcement-{Guid.NewGuid()}{extension}";
            await using var content = attachment.OpenReadStream();
            await fileStorage.SaveFileAsync(attachmentStoredFileName, content);
        }

        int newId;
        try
        {
            newId = await announcements.CreateAsync(
                schoolId,
                authorUserId,
                dto.Title,
                dto.Body,
                scope.ToString(),
                scope == AnnouncementScope.Grade ? dto.ScopeGradeId : null,
                scope == AnnouncementScope.Section ? dto.ScopeSectionId : null,
                status.ToString(),
                priority.ToString(),
                status == AnnouncementStatus.Scheduled ? dto.ScheduledForUtc : null,
                dto.ExpiresAtUtc,
                audience.RecipientUserIds.Count,
                dto.Attachment?.FileName,
                attachmentStoredFileName,
                dto.Attachment?.ContentType,
                dto.Attachment?.Length);
        }
        catch (PostgresException ex)
        {
            if (attachmentStoredFileName is not null && fileStorage is not null) await fileStorage.DeleteFileAsync(attachmentStoredFileName);
            logger.LogError(ex, "Failed to save announcement '{Title}'", dto.Title);
            return StatusCode(500, new { message = "Unable to save the announcement. Please try again." });
        }

        if (status == AnnouncementStatus.Sent)
        {
            await notifications.NotifyManyTrackedAsync(audience.RecipientUserIds, dto.Title, dto.Body, NotificationType.General, $"/notifications?announcementId={newId}", priority.ToString(), newId);
        }

        await audit.LogAsync("Create", "Announcement", newId.ToString(), $"Status={status}, Scope={scope}, Recipients={audience.RecipientUserIds.Count}");

        var created = await announcements.GetByIdAsync(newId, schoolId);
        return CreatedAtAction(nameof(GetRecent), null, created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<AnnouncementDto>> UpdateDraft(int id, AnnouncementUpdateDto dto)
    {
        if (currentTenant.SchoolId is not { } schoolId) return Forbid();
        var item = await announcements.GetDispatchItemAsync(id, schoolId);
        if (item is null) return NotFound();
        if (!CanManage(item)) return Forbid();
        if (item.Status != "Draft") return Conflict(new { message = "Only Draft announcements can be edited." });
        if (string.IsNullOrWhiteSpace(dto.Title) || string.IsNullOrWhiteSpace(dto.Body)) return BadRequest(new { message = "Title and Body are required." });
        if (!Enum.TryParse<AnnouncementPriority>(dto.Priority, true, out var priority)) return BadRequest(new { message = "Invalid priority." });
        await announcements.UpdateDraftAsync(id, dto.Title.Trim(), dto.Body.Trim(), priority.ToString(), dto.ExpiresAtUtc);
        await audit.LogAsync("Update", "Announcement", id.ToString(), "Draft edited");
        return Ok((await announcements.GetByIdAsync(id, schoolId))!);
    }

    [HttpPost("{id:int}/publish")]
    public async Task<ActionResult<AnnouncementDto>> Publish(int id)
    {
        if (currentTenant.SchoolId is not { } schoolId) return Forbid();
        var item = await announcements.GetDispatchItemAsync(id, schoolId);
        if (item is null) return NotFound();
        if (!CanManage(item)) return Forbid();
        if (item.Status is "Sent" or "Expired" or "Withdrawn") return Conflict(new { message = "Only Draft or Scheduled announcements can be sent." });
        if (item.ExpiresAtUtc is not null && item.ExpiresAtUtc <= DateTime.UtcNow) return Conflict(new { message = "This announcement has already expired." });

        var recipientUserIds = await ResolveRecipientUserIdsAsync(item);
        if (recipientUserIds.Count == 0) return BadRequest(new { message = "This audience has no active user recipients." });
        await notifications.NotifyManyTrackedAsync(recipientUserIds, item.Title, item.Body, NotificationType.General, $"/notifications?announcementId={item.Id}", item.Priority, item.Id);
        await announcements.MarkSentAsync(item.Id, recipientUserIds.Count);
        await audit.LogAsync("Publish", "Announcement", id.ToString(), $"Recipients={recipientUserIds.Count}");
        return Ok((await announcements.GetByIdAsync(id, schoolId))!);
    }

    [HttpPost("{id:int}/schedule")]
    public async Task<ActionResult<AnnouncementDto>> Schedule(int id, AnnouncementScheduleDto dto)
    {
        if (dto.ScheduledForUtc <= DateTime.UtcNow) return BadRequest(new { message = "Scheduled time must be in the future." });
        if (currentTenant.SchoolId is not { } schoolId) return Forbid();
        var item = await announcements.GetDispatchItemAsync(id, schoolId);
        if (item is null) return NotFound();
        if (!CanManage(item)) return Forbid();
        if (item.Status is "Sent" or "Expired" or "Withdrawn") return Conflict(new { message = "Only Draft or Scheduled announcements can be scheduled." });
        if (item.ExpiresAtUtc is not null && item.ExpiresAtUtc <= dto.ScheduledForUtc) return BadRequest(new { message = "Expiry must be later than delivery." });
        await announcements.SetLifecycleAsync(id, AnnouncementStatus.Scheduled.ToString(), dto.ScheduledForUtc);
        return Ok((await announcements.GetByIdAsync(id, schoolId))!);
    }

    [HttpPost("{id:int}/withdraw")]
    public async Task<ActionResult<AnnouncementDto>> Withdraw(int id)
    {
        if (currentTenant.SchoolId is not { } schoolId) return Forbid();
        var item = await announcements.GetDispatchItemAsync(id, schoolId);
        if (item is null) return NotFound();
        if (!CanManage(item)) return Forbid();
        await announcements.SetLifecycleAsync(id, AnnouncementStatus.Withdrawn.ToString());
        await audit.LogAsync("Withdraw", "Announcement", id.ToString());
        return Ok((await announcements.GetByIdAsync(id, schoolId))!);
    }

    [HttpPost("{id:int}/retry")]
    public async Task<ActionResult<AnnouncementDto>> Retry(int id)
    {
        if (currentTenant.SchoolId is not { } schoolId) return Forbid();
        var item = await announcements.GetDispatchItemAsync(id, schoolId);
        if (item is null) return NotFound();
        if (!CanManage(item)) return Forbid();
        await announcements.RetryAsync(id);
        await audit.LogAsync("Retry", "Announcement", id.ToString());
        return Ok((await announcements.GetByIdAsync(id, schoolId))!);
    }

    private async Task<(List<int> StudentIds, List<int> RecipientUserIds, string? Error)> ResolveAudienceAsync(
        string scopeValue,
        int? gradeId,
        int? sectionId,
        int schoolId)
    {
        if (!Enum.TryParse<AnnouncementScope>(scopeValue, true, out var scope)) return ([], [], "Invalid audience scope.");
        List<int> studentIds;
        if (scope == AnnouncementScope.Grade)
        {
            if (gradeId is null || !await referenceData.GradeExistsAsync(gradeId.Value, schoolId)) return ([], [], "Select a valid grade.");
            studentIds = (await students.GetActiveByGradeAsync(gradeId.Value, schoolId)).Select(student => student.Id).ToList();
        }
        else if (scope == AnnouncementScope.Section)
        {
            if (sectionId is null || !await sections.ExistsAsync(sectionId.Value, schoolId)) return ([], [], "Select a valid section.");
            if (currentUser.Role == "Teacher" && (currentUser.TeacherId is not { } teacherId || !await assignments.TeacherHasSectionAsync(teacherId, sectionId.Value, schoolId))) return ([], [], "You are not assigned to this section.");
            studentIds = (await students.GetActiveBySectionAsync(sectionId.Value, schoolId)).Select(student => student.Id).ToList();
        }
        else
        {
            if (currentUser.Role == "Teacher") return ([], [], "Teachers must select an assigned section.");
            studentIds = (await students.GetAllAsync(schoolId, StudentStatus.Active)).Select(student => student.Id).ToList();
        }
        return (studentIds, await recipients.ForStudentsAsync(studentIds), null);
    }

    private async Task<List<int>> ResolveRecipientUserIdsAsync(AnnouncementDispatchItem item)
    {
        List<int> studentIds = item.ScopeType switch
        {
            "Grade" when item.ScopeGradeId is { } gradeId => (await students.GetActiveByGradeAsync(gradeId, item.SchoolId)).Select(student => student.Id).ToList(),
            "Section" when item.ScopeSectionId is { } sectionId => (await students.GetActiveBySectionAsync(sectionId, item.SchoolId)).Select(student => student.Id).ToList(),
            _ => (await students.GetAllAsync(item.SchoolId, StudentStatus.Active)).Select(student => student.Id).ToList(),
        };
        return await recipients.ForStudentsAsync(studentIds);
    }

    private bool CanManage(AnnouncementDispatchItem item) => currentUser.Role == "Admin" || item.AuthorUserId == currentUser.UserId;
}
