using System.Text;
using StudentConnect.Infrastructure.Data.Stores;

namespace StudentConnect.Infrastructure.Notifications;

// The digest-mode counterpart to EmailOutboxProcessor: instead of one realtime email per
// notification, batches every not-yet-digested notification for a digest-enabled user into
// one summary email, on a slower polling interval (this is a periodic digest, not a queue).
public class NotificationDigestProcessor(IServiceScopeFactory scopeFactory, ILogger<NotificationDigestProcessor> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDigestsAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Notification digest processing loop failed");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown.
            }
        }
    }

    private async Task ProcessDigestsAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var notificationStore = scope.ServiceProvider.GetRequiredService<NotificationStore>();
        var users = scope.ServiceProvider.GetRequiredService<UserStore>();
        var emailOutbox = scope.ServiceProvider.GetRequiredService<EmailOutboxStore>();

        var userIds = await notificationStore.GetUserIdsWithPendingDigestAsync();
        foreach (var userId in userIds)
        {
            var pending = await notificationStore.GetUndigestedForUserAsync(userId);
            if (pending.Count == 0)
            {
                continue;
            }

            var email = await users.GetEmailForUserAsync(userId);
            if (!string.IsNullOrWhiteSpace(email))
            {
                var body = BuildDigestBody(pending.Select(n => (n.Title, n.Message)));
                await emailOutbox.EnqueueAsync(email, userId, $"Your StudentConnect digest ({pending.Count} update(s))", body);
            }

            await notificationStore.MarkDigestedAsync(pending.Select(n => n.Id).ToList());
        }
    }

    private static string BuildDigestBody(IEnumerable<(string Title, string Message)> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Here's a summary of your recent StudentConnect notifications:");
        sb.AppendLine();
        foreach (var (title, message) in items)
        {
            sb.AppendLine($"- {title}: {message}");
        }
        return sb.ToString();
    }
}
