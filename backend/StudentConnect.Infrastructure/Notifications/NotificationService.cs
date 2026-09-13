using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Data.Stores;

namespace StudentConnect.Infrastructure.Notifications;

// In-app "Local" implementation — writes rows the recipient sees via GET /api/notifications.
public class NotificationService(NotificationStore notificationStore) : INotificationService
{
    public async Task NotifyAsync(int userId, string title, string message, NotificationType type) =>
        await notificationStore.InsertManyAsync([userId], title, message, type);

    public async Task NotifyManyAsync(IEnumerable<int> userIds, string title, string message, NotificationType type) =>
        await notificationStore.InsertManyAsync(userIds, title, message, type);
}

