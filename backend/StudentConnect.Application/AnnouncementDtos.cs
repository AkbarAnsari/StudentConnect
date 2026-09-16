using Microsoft.AspNetCore.Http;

namespace StudentConnect.Application;

// A teacher/admin-authored broadcast — distinct from the system-generated Notification rows
// it fans out into (one row here is the audit/source record; many Notifications are created
// from it). See AnnouncementsController.
public class AnnouncementDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string ScopeType { get; set; } = string.Empty;
    public string? ScopeName { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateTime? ScheduledForUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public DateTime? WithdrawnAtUtc { get; set; }
    public int RecipientCount { get; set; }
    public int ReadCount { get; set; }
    public string? AttachmentFileName { get; set; }
    public long? AttachmentFileSize { get; set; }
    public int DispatchAttempts { get; set; }
    public string? LastDispatchError { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

// ScopeType is "School" | "Grade" | "Section" — ScopeGradeId/ScopeSectionId are required
// only for their matching scope (validated in AnnouncementsController).
public class AnnouncementCreateDto
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string ScopeType { get; set; } = string.Empty;
    public int? ScopeGradeId { get; set; }
    public int? ScopeSectionId { get; set; }
    public string Status { get; set; } = "Sent";
    public string Priority { get; set; } = "Normal";
    public DateTime? ScheduledForUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public IFormFile? Attachment { get; set; }
}

public class AnnouncementUpdateDto
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Priority { get; set; } = "Normal";
    public DateTime? ExpiresAtUtc { get; set; }
}

public class AnnouncementAudiencePreviewDto
{
    public string ScopeType { get; set; } = string.Empty;
    public int? ScopeGradeId { get; set; }
    public int? ScopeSectionId { get; set; }
}

public class AnnouncementScheduleDto
{
    public DateTime ScheduledForUtc { get; set; }
}

public class AnnouncementAudienceResultDto
{
    public int StudentCount { get; set; }
    public int RecipientCount { get; set; }
}
