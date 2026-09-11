using StudentConnect.Api.Data;
using StudentConnect.Api.Models;

namespace StudentConnect.Api.Notifications;

// In-app "Local" implementation — writes rows the recipient sees via GET /api/notifications.
// Saves immediately (its own SaveChangesAsync) so a notification failure never blocks or is
// entangled with the caller's own save of the homework/attendance/mark that triggered it.
public class NotificationService(StudentConnectDbContext db) : INotificationService
{
    public async Task NotifyAsync(int userId, string title, string message, NotificationType type)
    {
        db.Notifications.Add(new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    public async Task NotifyManyAsync(IEnumerable<int> userIds, string title, string message, NotificationType type)
    {
        var now = DateTime.UtcNow;
        foreach (var userId in userIds.Distinct())
        {
            db.Notifications.Add(new Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                Type = type,
                CreatedAtUtc = now,
            });
        }
        await db.SaveChangesAsync();
    }
}
