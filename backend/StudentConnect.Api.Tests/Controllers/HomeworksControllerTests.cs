using Microsoft.AspNetCore.Mvc;
using StudentConnect.Api.Controllers;
using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Data.Stores;
using StudentConnect.Infrastructure.Notifications;
using Xunit;

namespace StudentConnect.Api.Tests.Controllers;

// Runs against a real Postgres test database (see DatabaseFixture).
[Collection("Database")]
public class HomeworksControllerTests(DatabaseFixture fixture)
{
    private HomeworksController CreateController(ICurrentUserService currentUser) =>
        new(
            new HomeworkStore(fixture.Database),
            new HomeworkSubmissionStore(fixture.Database),
            new StudentStore(fixture.Database),
            new ReferenceDataStore(fixture.Database),
            new TeacherSubjectAssignmentStore(fixture.Database),
            new NotificationRecipientResolver(new UserStore(fixture.Database), new ParentStore(fixture.Database)),
            currentUser,
            new FakeCurrentTenantService(),
            NullNotificationService.Instance,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<HomeworksController>.Instance);

    private async Task<(int SubjectId, int GradeId, int TeacherId)> SeedHomeworkPrereqsAsync()
    {
        var db = fixture.Database;
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var gradeId = await db.ExecuteScalarAsync<int>(
            """INSERT INTO "Grades" ("Name", "SchoolId") VALUES (@name, 1) RETURNING "Id" """,
            p => p.AddWithValue("name", $"Grade-{suffix}"));

        var subjectId = await db.ExecuteScalarAsync<int>(
            """INSERT INTO "Subjects" ("Name", "Code", "SchoolId") VALUES (@name, @code, 1) RETURNING "Id" """,
            p =>
            {
                p.AddWithValue("name", $"Subject-{suffix}");
                p.AddWithValue("code", suffix);
            });

        var teacherId = await db.ExecuteScalarAsync<int>(
            """
            INSERT INTO "Teachers" ("TeacherId", "FirstName", "LastName", "Email", "SchoolId")
            VALUES (@code, 'Test', 'Teacher', @email, 1) RETURNING "Id"
            """,
            p =>
            {
                p.AddWithValue("code", $"TCH-{suffix}");
                p.AddWithValue("email", $"teacher-{suffix}@example.com");
            });

        return (subjectId, gradeId, teacherId);
    }

    private async Task<int> SeedHomeworkAsync(int subjectId, int gradeId, int teacherId, DateOnly? dueDate = null) =>
        await new HomeworkStore(fixture.Database).CreateAsync(new Homework
        {
            Title = "Test Homework",
            Description = "Desc",
            SubjectId = subjectId,
            GradeId = gradeId,
            TeacherId = teacherId,
            CreatedDate = DateOnly.FromDateTime(DateTime.UtcNow),
            DueDate = dueDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
        });

    [Fact]
    public async Task BulkUpdateDueDate_OwningTeacher_UpdatesAllAndReturnsCount()
    {
        var (subjectId, gradeId, teacherId) = await SeedHomeworkPrereqsAsync();
        var id1 = await SeedHomeworkAsync(subjectId, gradeId, teacherId);
        var id2 = await SeedHomeworkAsync(subjectId, gradeId, teacherId);
        var newDueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        var controller = CreateController(TestCurrentUser.Teacher(teacherId));

        var result = await controller.BulkUpdateDueDate(new HomeworkBulkDueDateDto { HomeworkIds = [id1, id2], DueDate = newDueDate });

        Assert.IsType<OkObjectResult>(result);
        var store = new HomeworkStore(fixture.Database);
        Assert.Equal(newDueDate, (await store.GetByIdAsync(id1))!.DueDate);
        Assert.Equal(newDueDate, (await store.GetByIdAsync(id2))!.DueDate);
    }

    [Fact]
    public async Task BulkUpdateDueDate_NonOwningTeacher_ReturnsForbidden()
    {
        var (subjectId, gradeId, teacherId) = await SeedHomeworkPrereqsAsync();
        var (_, _, otherTeacherId) = await SeedHomeworkPrereqsAsync();
        var id1 = await SeedHomeworkAsync(subjectId, gradeId, teacherId);
        var controller = CreateController(TestCurrentUser.Teacher(otherTeacherId));

        var result = await controller.BulkUpdateDueDate(new HomeworkBulkDueDateDto { HomeworkIds = [id1], DueDate = DateOnly.FromDateTime(DateTime.UtcNow) });

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task BulkUpdateDueDate_Admin_CanUpdateAnyTeachersHomework()
    {
        var (subjectId, gradeId, teacherId) = await SeedHomeworkPrereqsAsync();
        var id1 = await SeedHomeworkAsync(subjectId, gradeId, teacherId);
        var newDueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14));
        var controller = CreateController(TestCurrentUser.Admin());

        var result = await controller.BulkUpdateDueDate(new HomeworkBulkDueDateDto { HomeworkIds = [id1], DueDate = newDueDate });

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(newDueDate, (await new HomeworkStore(fixture.Database).GetByIdAsync(id1))!.DueDate);
    }

    [Fact]
    public async Task BulkUpdateDueDate_EmptyList_ReturnsBadRequest()
    {
        var controller = CreateController(TestCurrentUser.Admin());

        var result = await controller.BulkUpdateDueDate(new HomeworkBulkDueDateDto { HomeworkIds = [], DueDate = DateOnly.FromDateTime(DateTime.UtcNow) });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task BulkUpdateDueDate_NonexistentId_ReturnsBadRequest()
    {
        var controller = CreateController(TestCurrentUser.Admin());

        var result = await controller.BulkUpdateDueDate(new HomeworkBulkDueDateDto { HomeworkIds = [999_999], DueDate = DateOnly.FromDateTime(DateTime.UtcNow) });

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
