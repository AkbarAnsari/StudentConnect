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

    Task NotifyTrackedAsync(int userId, string title, string message, NotificationType type, string? actionUrl, string priority, int? announcementId) =>
        NotifyAsync(userId, title, message, type);

    Task NotifyManyTrackedAsync(IEnumerable<int> userIds, string title, string message, NotificationType type, string? actionUrl, string priority, int? announcementId) =>
        NotifyManyAsync(userIds, title, message, type);

    async Task<IReadOnlyList<int>> NotifyManyForHomeworkAsync(IEnumerable<int> userIds, string title, string message, NotificationType type, string actionUrl, int homeworkId)
    {
        var ids = userIds.Distinct().ToList();
        await NotifyManyAsync(ids, title, message, type);
        return ids;
    }

    async Task<IReadOnlyList<int>> NotifyManyForLearningMaterialAsync(IEnumerable<int> userIds, string title, string message, NotificationType type, string actionUrl, int learningMaterialId)
    {
        var ids = userIds.Distinct().ToList();
        await NotifyManyAsync(ids, title, message, type);
        return ids;
    }
}
