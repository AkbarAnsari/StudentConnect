using StudentConnect.Application;
using StudentConnect.Domain;

namespace StudentConnect.Api.Tests;

// No-op stand-in for tests that construct a controller directly and don't care about actual
// notification delivery — mirrors NullAuditService's role for IAuditService.
public class NullNotificationService : INotificationService
{
    public static readonly NullNotificationService Instance = new();

    public Task NotifyAsync(int userId, string title, string message, NotificationType type) => Task.CompletedTask;

    public Task NotifyManyAsync(IEnumerable<int> userIds, string title, string message, NotificationType type) => Task.CompletedTask;
}
