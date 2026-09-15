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
public class ActivityRegistrationsControllerTests(DatabaseFixture fixture)
{
    private ActivityRegistrationsController CreateController(ICurrentUserService currentUser) =>
        new(
            new ActivityRegistrationStore(fixture.Database),
            new StudentStore(fixture.Database),
            new ActivityStore(fixture.Database),
            fixture.Database,
            new NotificationRecipientResolver(new UserStore(fixture.Database), new ParentStore(fixture.Database)),
            NullNotificationService.Instance,
            currentUser,
            new FakeCurrentTenantService(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ActivityRegistrationsController>.Instance);

    private async Task<(int GradeId, int StudentId)> SeedStudentAsync(int? gradeId = null)
    {
        var db = fixture.Database;
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var resolvedGradeId = gradeId ?? await db.ExecuteScalarAsync<int>(
            """INSERT INTO "Grades" ("Name", "SchoolId") VALUES (@name, 1) RETURNING "Id" """,
            p => p.AddWithValue("name", $"Grade-{suffix}"));

        var studentId = await db.ExecuteScalarAsync<int>(
            """
            INSERT INTO "Students" ("StudentId", "FirstName", "LastName", "Email", "GradeId", "SchoolId")
            VALUES (@code, 'Test', 'Student', @email, @gradeId, 1) RETURNING "Id"
            """,
            p =>
            {
                p.AddWithValue("code", $"STU-{suffix}");
                p.AddWithValue("email", $"{suffix}@example.com");
                p.AddWithValue("gradeId", resolvedGradeId);
            });

        return (resolvedGradeId, studentId);
    }

    private async Task<int> SeedActivityAsync(int gradeId, int capacity, DateTime? date = null) =>
        await new ActivityStore(fixture.Database).CreateAsync(new Activity
        {
            Title = "Test Activity",
            Description = "Desc",
            Date = date ?? DateTime.UtcNow.AddDays(7),
            Location = "Hall",
            GradeId = gradeId,
            Capacity = capacity,
            CreatedDate = DateTime.UtcNow,
        });

    [Fact]
    public async Task Register_WithinCapacity_CreatesRegisteredStatus()
    {
        var (gradeId, studentId) = await SeedStudentAsync();
        var activityId = await SeedActivityAsync(gradeId, capacity: 5);
        var controller = CreateController(TestCurrentUser.Student(studentId));

        var result = await controller.Register(new ActivityRegistrationCreateDto { ActivityId = activityId });

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var dto = Assert.IsType<ActivityRegistrationDto>(created.Value);
        Assert.Equal("Registered", dto.Status);
    }

    [Fact]
    public async Task Register_AtCapacity_WaitlistsInstead()
    {
        var (gradeId, studentId1) = await SeedStudentAsync();
        var (_, studentId2) = await SeedStudentAsync(gradeId);
        var activityId = await SeedActivityAsync(gradeId, capacity: 1);

        var firstResult = await CreateController(TestCurrentUser.Student(studentId1))
            .Register(new ActivityRegistrationCreateDto { ActivityId = activityId });
        Assert.IsType<CreatedAtActionResult>(firstResult.Result);

        var secondResult = await CreateController(TestCurrentUser.Student(studentId2))
            .Register(new ActivityRegistrationCreateDto { ActivityId = activityId });

        var created = Assert.IsType<CreatedAtActionResult>(secondResult.Result);
        var dto = Assert.IsType<ActivityRegistrationDto>(created.Value);
        Assert.Equal("Waitlisted", dto.Status);
    }

    [Fact]
    public async Task Register_AlreadyRegistered_ReturnsConflict()
    {
        var (gradeId, studentId) = await SeedStudentAsync();
        var activityId = await SeedActivityAsync(gradeId, capacity: 5);
        var controller = CreateController(TestCurrentUser.Student(studentId));

        await controller.Register(new ActivityRegistrationCreateDto { ActivityId = activityId });
        var result = await controller.Register(new ActivityRegistrationCreateDto { ActivityId = activityId });

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task CancelRegistration_FreesSeat_PromotesEarliestWaitlisted()
    {
        var (gradeId, studentId1) = await SeedStudentAsync();
        var (_, studentId2) = await SeedStudentAsync(gradeId);
        var activityId = await SeedActivityAsync(gradeId, capacity: 1);

        var firstResult = await CreateController(TestCurrentUser.Student(studentId1))
            .Register(new ActivityRegistrationCreateDto { ActivityId = activityId });
        var firstCreated = Assert.IsType<CreatedAtActionResult>(firstResult.Result);
        var firstDto = Assert.IsType<ActivityRegistrationDto>(firstCreated.Value);

        var secondResult = await CreateController(TestCurrentUser.Student(studentId2))
            .Register(new ActivityRegistrationCreateDto { ActivityId = activityId });
        var secondCreated = Assert.IsType<CreatedAtActionResult>(secondResult.Result);
        var secondDto = Assert.IsType<ActivityRegistrationDto>(secondCreated.Value);
        Assert.Equal("Waitlisted", secondDto.Status);

        var cancelResult = await CreateController(TestCurrentUser.Student(studentId1)).CancelRegistration(firstDto.Id);
        Assert.IsType<OkObjectResult>(cancelResult.Result);

        var registrations = new ActivityRegistrationStore(fixture.Database);
        var promoted = await registrations.GetByIdAsync(secondDto.Id);
        Assert.Equal(ActivityRegistrationStatus.Registered, promoted!.Status);
    }

    [Fact]
    public async Task Register_ActivityFromAnotherSchool_ReturnsBadRequest()
    {
        var db = fixture.Database;
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var otherSchoolId = await db.ExecuteScalarAsync<int>(
            """INSERT INTO "Schools" ("Name", "Code", "IsActive") VALUES (@name, @code, true) RETURNING "Id" """,
            p =>
            {
                p.AddWithValue("name", $"Other-{suffix}");
                p.AddWithValue("code", suffix);
            });
        var otherGradeId = await db.ExecuteScalarAsync<int>(
            """INSERT INTO "Grades" ("Name", "SchoolId") VALUES (@name, @schoolId) RETURNING "Id" """,
            p =>
            {
                p.AddWithValue("name", $"Grade-{suffix}");
                p.AddWithValue("schoolId", otherSchoolId);
            });
        var otherActivityId = await SeedActivityAsync(otherGradeId, capacity: 5);

        var (_, studentId) = await SeedStudentAsync();
        var controller = CreateController(TestCurrentUser.Student(studentId));

        var result = await controller.Register(new ActivityRegistrationCreateDto { ActivityId = otherActivityId });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }
}
