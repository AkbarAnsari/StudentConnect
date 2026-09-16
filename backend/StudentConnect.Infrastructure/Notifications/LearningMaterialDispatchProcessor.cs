using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Data.Stores;

namespace StudentConnect.Infrastructure.Notifications;

public class LearningMaterialDispatchProcessor(
    IServiceScopeFactory scopeFactory,
    ILogger<LearningMaterialDispatchProcessor> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessDueAsync(); }
            catch (Exception ex) { logger.LogError(ex, "Scheduled learning material processing failed"); }

            try { await Task.Delay(PollInterval, stoppingToken); }
            catch (OperationCanceledException) { }
        }
    }

    private async Task ProcessDueAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var materials = scope.ServiceProvider.GetRequiredService<LearningMaterialStore>();
        var students = scope.ServiceProvider.GetRequiredService<StudentStore>();
        var recipients = scope.ServiceProvider.GetRequiredService<NotificationRecipientResolver>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

        foreach (var item in await materials.GetDueScheduledAsync())
        {
            try
            {
                var studentRows = item.SectionId is { } sectionId
                    ? await students.GetActiveBySectionAndSubjectAsync(sectionId, item.SubjectId, item.Grade!.SchoolId)
                    : await students.GetActiveByGradeAndSubjectAsync(item.GradeId, item.SubjectId, item.Grade!.SchoolId);
                foreach (var student in studentRows)
                {
                    var userIds = await recipients.ForStudentAsync(student.Id);
                    await notifications.NotifyManyForLearningMaterialAsync(
                        userIds,
                        "New learning material",
                        $"{item.Subject!.Name}: {item.Title}",
                        NotificationType.LearningMaterialReleased,
                        $"/students/{student.Id}/materials",
                        item.Id);
                }
                await materials.SetLifecycleAsync(item.Id, ContentLifecycleStatus.Published);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to dispatch scheduled learning material {LearningMaterialId}", item.Id);
            }
        }
    }
}