using StudentConnect.Domain;

namespace StudentConnect.Application;

public class NotificationResponseDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string Priority { get; set; } = "Normal";
    public string? ActionUrl { get; set; }
    public int? AnnouncementId { get; set; }
    public int? HomeworkId { get; set; }
    public int? LearningMaterialId { get; set; }

    public static NotificationResponseDto FromEntity(Notification n) => new()
    {
        Id = n.Id,
        Title = n.Title,
        Message = n.Message,
        Type = n.Type.ToString(),
        IsRead = n.IsRead,
        CreatedAtUtc = n.CreatedAtUtc,
        Priority = n.Priority,
        ActionUrl = n.ActionUrl,
        AnnouncementId = n.AnnouncementId,
        HomeworkId = n.HomeworkId,
        LearningMaterialId = n.LearningMaterialId,
    };
}
