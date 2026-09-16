using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Data.Stores;

namespace StudentConnect.Infrastructure.Notifications;

public class HomeworkDispatchProcessor(
    IServiceScopeFactory scopeFactory,
    ILogger<HomeworkDispatchProcessor> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessDueAsync(); }
            catch (Exception ex) { logger.LogError(ex, "Scheduled homework processing failed"); }

            try { await Task.Delay(PollInterval, stoppingToken); }
            catch (OperationCanceledException) { }
        }
    }

    private async Task ProcessDueAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var homeworkStore = scope.ServiceProvider.GetRequiredService<HomeworkStore>();
        var students = scope.ServiceProvider.GetRequiredService<StudentStore>();
        var recipients = scope.ServiceProvider.GetRequiredService<NotificationRecipientResolver>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

        foreach (var item in await homeworkStore.GetDueScheduledAsync())
        {
            try
            {
                var studentRows = item.SectionId is { } sectionId
                    ? await students.GetActiveBySectionAndSubjectAsync(sectionId, item.SubjectId, item.Grade!.SchoolId)
                    : await students.GetActiveByGradeAndSubjectAsync(item.GradeId, item.SubjectId, item.Grade!.SchoolId);
                foreach (var student in studentRows)
                {
                    var userIds = await recipients.ForStudentAsync(student.Id);
                    await notifications.NotifyManyForHomeworkAsync(
                        userIds,
                        "New homework assigned",
                        $"{item.Subject!.Name}: {item.Title} (due {item.DueDate:yyyy-MM-dd})",
                        NotificationType.HomeworkAssigned,
                        $"/students/{student.Id}/homework/{item.Id}",
                        item.Id);
                }
                await homeworkStore.SetLifecycleAsync(item.Id, ContentLifecycleStatus.Published);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to dispatch scheduled homework {HomeworkId}", item.Id);
            }
        }
    }
}