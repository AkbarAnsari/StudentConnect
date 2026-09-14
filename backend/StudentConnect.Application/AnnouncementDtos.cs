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
}
