using StudentConnect.Api.Data.Stores;
using StudentConnect.Api.Models;

namespace StudentConnect.Api.Notifications;

// Decorates the plain in-app NotificationService with email delivery — a new provider of
// the same INotificationService contract, per the phase-20 roadmap, rather than modifying
// NotificationService itself. Emails are enqueued (see EmailOutboxStore), never sent inline,
// so a slow/unavailable SMTP server can never block the request that triggered the notification.
public class EmailNotificationService(NotificationService inApp, EmailOutboxStore emailOutbox, UserStore users) : INotificationService
{
    public async Task NotifyAsync(int userId, string title, string message, NotificationType type)
    {
        await inApp.NotifyAsync(userId, title, message, type);
        await EnqueueEmailAsync(userId, title, message);
    }

    public async Task NotifyManyAsync(IEnumerable<int> userIds, string title, string message, NotificationType type)
    {
        var ids = userIds.ToList();
        await inApp.NotifyManyAsync(ids, title, message, type);
        foreach (var userId in ids)
        {
            await EnqueueEmailAsync(userId, title, message);
        }
    }

    private async Task EnqueueEmailAsync(int userId, string title, string message)
    {
        var email = await users.GetEmailForUserAsync(userId);
        if (!string.IsNullOrWhiteSpace(email))
        {
            await emailOutbox.EnqueueAsync(email, userId, title, message);
        }
    }
}
