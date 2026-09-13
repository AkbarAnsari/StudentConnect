using StudentConnect.Domain;

namespace StudentConnect.Application;

// Same pattern as IFileStorage (Phase 13): controllers depend only on this interface and never
// know which delivery mechanism is active. Today the only implementation writes an in-app
// "outbox" row (NotificationService below); a future EmailNotificationService could implement
// the same interface to also send email/SMS, selected via config exactly like Storage:Provider.
public interface INotificationService
{
    Task NotifyAsync(int userId, string title, string message, NotificationType type);

    Task NotifyManyAsync(IEnumerable<int> userIds, string title, string message, NotificationType type);
}
