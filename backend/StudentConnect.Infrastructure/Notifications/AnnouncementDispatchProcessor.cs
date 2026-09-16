using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Data.Stores;

namespace StudentConnect.Infrastructure.Notifications;

public class AnnouncementDispatchProcessor(
    IServiceScopeFactory scopeFactory,
    ILogger<AnnouncementDispatchProcessor> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduled announcement processing failed");
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

    private async Task ProcessDueAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var announcements = scope.ServiceProvider.GetRequiredService<AnnouncementStore>();
        var students = scope.ServiceProvider.GetRequiredService<StudentStore>();
        var recipients = scope.ServiceProvider.GetRequiredService<NotificationRecipientResolver>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

        await announcements.ExpireDueAsync();
        foreach (var item in await announcements.GetDueScheduledAsync())
        {
            try
            {
                List<int> studentIds = item.ScopeType switch
                {
                    "Grade" when item.ScopeGradeId is { } gradeId =>
                        (await students.GetActiveByGradeAsync(gradeId, item.SchoolId)).Select(student => student.Id).ToList(),
                    "Section" when item.ScopeSectionId is { } sectionId =>
                        (await students.GetActiveBySectionAsync(sectionId, item.SchoolId)).Select(student => student.Id).ToList(),
                    _ => (await students.GetAllAsync(item.SchoolId, StudentStatus.Active)).Select(student => student.Id).ToList(),
                };
                var userIds = await recipients.ForStudentsAsync(studentIds);
                await notifications.NotifyManyTrackedAsync(userIds, item.Title, item.Body, NotificationType.General, $"/notifications?announcementId={item.Id}", item.Priority, item.Id);
                await announcements.MarkSentAsync(item.Id, userIds.Count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to dispatch scheduled announcement {AnnouncementId}", item.Id);
                await announcements.MarkDispatchFailedAsync(item.Id, ex.Message);
            }
        }
    }
}