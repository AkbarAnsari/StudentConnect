namespace StudentConnect.Domain;

// One row per (recipient, event) — this is the "outbox" for Phase 14 in-app notifications.
// A future email/SMS provider would read from the same trigger points (see
// Notifications/INotificationService.cs) rather than a different table.
public class Notification
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string Priority { get; set; } = "Normal";
    public string? ActionUrl { get; set; }
    public int? AnnouncementId { get; set; }
    public int? HomeworkId { get; set; }
    public int? LearningMaterialId { get; set; }

    // Set once this notification has been folded into a digest email (see
    // NotificationDigestProcessor) — null means still pending for a digest-enabled user.
    public DateTime? DigestedAtUtc { get; set; }
}
