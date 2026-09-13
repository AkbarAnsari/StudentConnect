using StudentConnect.Api.Data.Stores;
using StudentConnect.Api.Email;

namespace StudentConnect.Api.Notifications;

// The other half of the outbox pattern — polls for unsent EmailOutbox rows and delivers
// them via IEmailSender. Runs on a fixed interval rather than being woken up by an enqueue
// (no in-process signaling, no external queue) — simple and good enough at this volume;
// an actual queue (Azure Service Bus, etc.) would replace the polling loop, not this class's
// responsibilities, if throughput ever demanded it.
public class EmailOutboxProcessor(IServiceScopeFactory scopeFactory, ILogger<EmailOutboxProcessor> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);
    private const int BatchSize = 20;
    private const int MaxAttempts = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Email outbox processing loop failed");
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

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<EmailOutboxStore>();
        var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        var pending = await outbox.GetPendingAsync(BatchSize, MaxAttempts);
        foreach (var entry in pending)
        {
            try
            {
                await sender.SendAsync(entry.ToEmail, entry.Subject, entry.Body, ct);
                await outbox.MarkSentAsync(entry.Id);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send email {Id} to {ToEmail}", entry.Id, entry.ToEmail);
                await outbox.MarkFailedAsync(entry.Id, ex.Message);
            }
        }
    }
}
