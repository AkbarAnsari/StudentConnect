using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Data.Stores;

namespace StudentConnect.Infrastructure.Notifications;

// Decorates the plain in-app NotificationService with email delivery — a new provider of
// the same INotificationService contract, per the phase-20 roadmap, rather than modifying
// NotificationService itself. Emails are enqueued (see EmailOutboxStore), never sent inline,
// so a slow/unavailable SMTP server can never block the request that triggered the notification.
public class EmailNotificationService(NotificationService inApp, EmailOutboxStore emailOutbox, UserStore users) : INotificationService
{
    public async Task NotifyAsync(int userId, string title, string message, NotificationType type)
    {
        var preference = await users.GetNotificationPreferencesAsync(userId);
        if (preference.InAppChannelEnabled) await inApp.NotifyAsync(userId, title, message, type);
        await EnqueueEmailAsync(userId, title, message, preference);
    }

    public async Task NotifyManyAsync(IEnumerable<int> userIds, string title, string message, NotificationType type)
    {
        var preferences = await GetPreferencesAsync(userIds);
        await inApp.NotifyManyAsync(preferences.Where(item => item.Value.InAppChannelEnabled).Select(item => item.Key), title, message, type);
        foreach (var (userId, preference) in preferences)
        {
            await EnqueueEmailAsync(userId, title, message, preference);
        }
    }

    public async Task NotifyTrackedAsync(int userId, string title, string message, NotificationType type, string? actionUrl, string priority, int? announcementId)
    {
        var preference = await users.GetNotificationPreferencesAsync(userId);
        if (preference.InAppChannelEnabled) await inApp.NotifyTrackedAsync(userId, title, message, type, actionUrl, priority, announcementId);
        await EnqueueEmailAsync(userId, title, message, preference);
    }

    public async Task NotifyManyTrackedAsync(IEnumerable<int> userIds, string title, string message, NotificationType type, string? actionUrl, string priority, int? announcementId)
    {
        var preferences = await GetPreferencesAsync(userIds);
        await inApp.NotifyManyTrackedAsync(preferences.Where(item => item.Value.InAppChannelEnabled).Select(item => item.Key), title, message, type, actionUrl, priority, announcementId);
        foreach (var (userId, preference) in preferences) await EnqueueEmailAsync(userId, title, message, preference);
    }

    public async Task<IReadOnlyList<int>> NotifyManyForHomeworkAsync(IEnumerable<int> userIds, string title, string message, NotificationType type, string actionUrl, int homeworkId)
    {
        var preferences = await GetPreferencesAsync(userIds);
        var insertedIds = await inApp.NotifyManyForHomeworkAsync(preferences.Where(item => item.Value.InAppChannelEnabled).Select(item => item.Key), title, message, type, actionUrl, homeworkId);
        foreach (var userId in insertedIds) await EnqueueEmailAsync(userId, title, message, preferences[userId]);
        return insertedIds;
    }

    public async Task<IReadOnlyList<int>> NotifyManyForLearningMaterialAsync(IEnumerable<int> userIds, string title, string message, NotificationType type, string actionUrl, int learningMaterialId)
    {
        var preferences = await GetPreferencesAsync(userIds);
        var insertedIds = await inApp.NotifyManyForLearningMaterialAsync(preferences.Where(item => item.Value.InAppChannelEnabled).Select(item => item.Key), title, message, type, actionUrl, learningMaterialId);
        foreach (var userId in insertedIds) await EnqueueEmailAsync(userId, title, message, preferences[userId]);
        return insertedIds;
    }

    private async Task EnqueueEmailAsync(int userId, string title, string message, NotificationPreferenceDto preference)
    {
        // Digest-enabled users (Parents, typically) get their emails batched by
        // NotificationDigestProcessor instead of one realtime email per event.
        if (!preference.EmailChannelEnabled || preference.DigestEnabled || IsQuietHours(preference))
        {
            return;
        }

        var email = await users.GetEmailForUserAsync(userId);
        if (!string.IsNullOrWhiteSpace(email))
        {
            await emailOutbox.EnqueueAsync(email, userId, title, message);
        }
    }

    private async Task<Dictionary<int, NotificationPreferenceDto>> GetPreferencesAsync(IEnumerable<int> userIds)
    {
        var result = new Dictionary<int, NotificationPreferenceDto>();
        foreach (var userId in userIds.Distinct()) result[userId] = await users.GetNotificationPreferencesAsync(userId);
        return result;
    }

    private static bool IsQuietHours(NotificationPreferenceDto preference)
    {
        if (preference.QuietHoursStart is not { } start || preference.QuietHoursEnd is not { } end || start == end) return false;
        var now = TimeOnly.FromDateTime(DateTime.UtcNow);
        return start < end ? now >= start && now < end : now >= start || now < end;
    }
}
