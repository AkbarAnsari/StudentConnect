using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Data.Stores;
using StudentConnect.Infrastructure.Notifications;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/announcements")]
[Authorize(Roles = "Teacher,Admin")]
public class AnnouncementsController(
    AnnouncementStore announcements,
    StudentStore students,
    ReferenceDataStore referenceData,
    SectionStore sections,
    NotificationRecipientResolver recipients,
    INotificationService notifications,
    ICurrentUserService currentUser,
    ICurrentTenantService currentTenant,
    IAuditService audit,
    ILogger<AnnouncementsController> logger) : ControllerBase
{
    // GET /api/announcements?take=20
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AnnouncementDto>>> GetRecent(int take = 20)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        return Ok(await announcements.GetRecentAsync(schoolId, Math.Clamp(take, 1, 100)));
    }

    // POST /api/announcements — Teacher may target any Grade/Section/the whole School within
    // their own School, matching this codebase's existing, deliberately permissive Teacher-trust
    // model (any Teacher can already create Homework for any Grade in their school).
    [HttpPost]
    public async Task<ActionResult<AnnouncementDto>> CreateAnnouncement(AnnouncementCreateDto dto)
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

        List<int> studentIds;
        switch (scope)
        {
            case AnnouncementScope.Grade:
                if (dto.ScopeGradeId is not { } gradeId || !await referenceData.GradeExistsAsync(gradeId, schoolId))
                {
                    return BadRequest(new { message = "A valid ScopeGradeId is required for a Grade-scoped announcement." });
                }
                studentIds = (await students.GetByGradeOrderedByFirstNameAsync(gradeId)).Select(s => s.Id).ToList();
                break;

            case AnnouncementScope.Section:
                if (dto.ScopeSectionId is not { } sectionId || !await sections.ExistsAsync(sectionId, schoolId))
                {
                    return BadRequest(new { message = "A valid ScopeSectionId is required for a Section-scoped announcement." });
                }
                studentIds = (await students.GetBySectionAsync(sectionId)).Select(s => s.Id).ToList();
                break;

            default:
                studentIds = (await students.GetAllAsync(schoolId, StudentStatus.Active)).Select(s => s.Id).ToList();
                break;
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
                scope == AnnouncementScope.Section ? dto.ScopeSectionId : null);
        }
        catch (PostgresException ex)
        {
            logger.LogError(ex, "Failed to save announcement '{Title}'", dto.Title);
            return StatusCode(500, new { message = "Unable to save the announcement. Please try again." });
        }

        var recipientUserIds = await recipients.ForStudentsAsync(studentIds);
        if (recipientUserIds.Count > 0)
        {
            await notifications.NotifyManyAsync(recipientUserIds, dto.Title, dto.Body, NotificationType.General);
        }

        await audit.LogAsync("Create", "Announcement", newId.ToString(), $"Scope={scope}, Recipients={recipientUserIds.Count}");

        var created = await announcements.GetByIdAsync(newId, schoolId);
        return CreatedAtAction(nameof(GetRecent), null, created);
    }
}
