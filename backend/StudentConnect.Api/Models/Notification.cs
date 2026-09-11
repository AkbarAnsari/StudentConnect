namespace StudentConnect.Api.Models;

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
}
