using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StudentConnect.Api.Controllers;
using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Data.Stores;
using StudentConnect.Infrastructure.Notifications;
using StudentConnect.Infrastructure.Storage;
using Xunit;

namespace StudentConnect.Api.Tests.Controllers;

[Collection("Database")]
public class ContentTenantIsolationTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task Search_ReturnsOnlyCurrentSchoolsContent()
    {
        var seeded = await SeedForeignContentAsync();
        var store = new SearchStore(fixture.Database);

        var schoolOneResults = await store.SearchAsync(seeded.SearchTerm, 20, 1);
        var foreignSchoolResults = await store.SearchAsync(seeded.SearchTerm, 20, seeded.SchoolId);

        Assert.Empty(schoolOneResults);
        Assert.Equal(
            ["Activity", "Homework", "LearningMaterial", "Student", "Teacher"],
            foreignSchoolResults.Select(result => result.EntityType).Order().ToArray());
    }

    [Fact]
    public async Task Homework_RejectsForeignReadsWritesAndReferences()
    {
        var seeded = await SeedForeignContentAsync();
        var controller = CreateHomeworksController();

        var list = await controller.GetHomework();
        var listValue = Assert.IsType<OkObjectResult>(list.Result).Value;
        Assert.DoesNotContain(Assert.IsAssignableFrom<IEnumerable<HomeworkDto>>(listValue), item => item.Id == seeded.HomeworkId);

        var detail = await controller.GetHomeworkById(seeded.HomeworkId);
        Assert.IsType<NotFoundResult>(detail.Result);

        var create = await controller.CreateHomework(new HomeworkCreateDto
        {
            Title = "Cross-tenant attempt",
            Description = "Must fail",
            SubjectId = seeded.SubjectId,
            GradeId = seeded.GradeId,
            TeacherId = seeded.TeacherId,
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
        });
        Assert.IsType<BadRequestObjectResult>(create.Result);

        Assert.IsType<NotFoundResult>(await controller.DeleteHomework(seeded.HomeworkId));
        Assert.NotNull(await new HomeworkStore(fixture.Database).GetByIdAsync(seeded.HomeworkId, seeded.SchoolId));
    }

    [Fact]
    public async Task LearningMaterials_RejectForeignMetadataFilesWritesAndReferences()
    {
        var seeded = await SeedForeignContentAsync();
        var storage = new TrackingFileStorage();
        var controller = new LearningMaterialsController(
            new LearningMaterialStore(fixture.Database),
            new StudentStore(fixture.Database),
            new ReferenceDataStore(fixture.Database),
            new TeacherSubjectAssignmentStore(fixture.Database),
            storage,
            Options.Create(new FileStorageOptions()),
            TestCurrentUser.Admin(),
            new FakeCurrentTenantService(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<LearningMaterialsController>.Instance);

        var list = await controller.GetLearningMaterials(null, null);
        var listValue = Assert.IsType<OkObjectResult>(list.Result).Value;
        Assert.DoesNotContain(Assert.IsAssignableFrom<IEnumerable<LearningMaterialDto>>(listValue), item => item.Id == seeded.MaterialId);

        var detail = await controller.GetLearningMaterialById(seeded.MaterialId);
        Assert.IsType<NotFoundResult>(detail.Result);
        Assert.IsType<NotFoundObjectResult>(await controller.DownloadLearningMaterial(seeded.MaterialId));

        var create = await controller.UploadLearningMaterial(new LearningMaterialUploadRequest
        {
            Title = "Cross-tenant attempt",
            Description = "Must fail",
            SubjectId = seeded.SubjectId,
            GradeId = seeded.GradeId,
            TeacherId = seeded.TeacherId,
        });
        Assert.IsType<BadRequestObjectResult>(create.Result);

        Assert.IsType<NotFoundResult>(await controller.DeleteLearningMaterial(seeded.MaterialId));
        Assert.Empty(storage.DeletedFileNames);
        Assert.NotNull(await new LearningMaterialStore(fixture.Database).GetByIdAsync(seeded.MaterialId, seeded.SchoolId));
    }

    [Fact]
    public async Task Activities_RejectForeignReadsWritesAndReferences()
    {
        var seeded = await SeedForeignContentAsync();
        var controller = new ActivitiesController(
            new ActivityStore(fixture.Database),
            new ActivityRegistrationStore(fixture.Database),
            new ReferenceDataStore(fixture.Database),
            new FakeCurrentTenantService(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ActivitiesController>.Instance);

        var list = await controller.GetActivities();
        var listValue = Assert.IsType<OkObjectResult>(list.Result).Value;
        Assert.DoesNotContain(Assert.IsAssignableFrom<IEnumerable<ActivityDto>>(listValue), item => item.Id == seeded.ActivityId);

        var detail = await controller.GetActivityById(seeded.ActivityId);
        Assert.IsType<NotFoundResult>(detail.Result);

        var create = await controller.CreateActivity(new ActivityCreateDto
        {
            Title = "Cross-tenant attempt",
            Description = "Must fail",
            Date = DateTime.UtcNow.AddDays(1),
            Location = "Elsewhere",
            GradeId = seeded.GradeId,
            Capacity = 10,
        });
        Assert.IsType<BadRequestObjectResult>(create.Result);

        Assert.IsType<NotFoundResult>(await controller.DeleteActivity(seeded.ActivityId));
        Assert.NotNull(await new ActivityStore(fixture.Database).GetByIdAsync(seeded.ActivityId, seeded.SchoolId));
    }

    private HomeworksController CreateHomeworksController() =>
        new(
            new HomeworkStore(fixture.Database),
            new HomeworkSubmissionStore(fixture.Database),
            new StudentStore(fixture.Database),
            new ReferenceDataStore(fixture.Database),
            new TeacherSubjectAssignmentStore(fixture.Database),
            new NotificationRecipientResolver(new UserStore(fixture.Database), new ParentStore(fixture.Database)),
            TestCurrentUser.Admin(),
            new FakeCurrentTenantService(),
            NullNotificationService.Instance,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<HomeworksController>.Instance);

    private async Task<ForeignContent> SeedForeignContentAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var searchTerm = $"foreign{suffix}";
        var schoolId = await fixture.Database.ExecuteScalarAsync<int>(
            """INSERT INTO "Schools" ("Name", "Code", "IsActive") VALUES (@name, @code, true) RETURNING "Id" """,
            parameters =>
            {
                parameters.AddWithValue("name", $"Foreign School {suffix}");
                parameters.AddWithValue("code", $"foreign-{suffix}");
            });
        var gradeId = await fixture.Database.ExecuteScalarAsync<int>(
            """INSERT INTO "Grades" ("Name", "SchoolId") VALUES (@name, @schoolId) RETURNING "Id" """,
            parameters =>
            {
                parameters.AddWithValue("name", $"Grade {suffix}");
                parameters.AddWithValue("schoolId", schoolId);
            });
        var subjectId = await fixture.Database.ExecuteScalarAsync<int>(
            """INSERT INTO "Subjects" ("Name", "Code", "SchoolId") VALUES (@name, @code, @schoolId) RETURNING "Id" """,
            parameters =>
            {
                parameters.AddWithValue("name", $"Subject {suffix}");
                parameters.AddWithValue("code", suffix);
                parameters.AddWithValue("schoolId", schoolId);
            });
        var teacherId = await fixture.Database.ExecuteScalarAsync<int>(
            """
            INSERT INTO "Teachers" ("TeacherId", "FirstName", "LastName", "Email", "SchoolId")
            VALUES (@code, @firstName, 'Teacher', @email, @schoolId)
            RETURNING "Id"
            """,
            parameters =>
            {
                parameters.AddWithValue("code", $"T-{suffix}");
                parameters.AddWithValue("firstName", searchTerm);
                parameters.AddWithValue("email", $"teacher-{suffix}@example.com");
                parameters.AddWithValue("schoolId", schoolId);
            });
        await fixture.Database.ExecuteScalarAsync<int>(
            """
            INSERT INTO "Students" ("StudentId", "FirstName", "LastName", "Email", "GradeId", "SchoolId")
            VALUES (@code, @firstName, 'Student', @email, @gradeId, @schoolId)
            RETURNING "Id"
            """,
            parameters =>
            {
                parameters.AddWithValue("code", $"S-{suffix}");
                parameters.AddWithValue("firstName", searchTerm);
                parameters.AddWithValue("email", $"student-{suffix}@example.com");
                parameters.AddWithValue("gradeId", gradeId);
                parameters.AddWithValue("schoolId", schoolId);
            });

        var homeworkId = await new HomeworkStore(fixture.Database).CreateAsync(new Homework
        {
            Title = searchTerm,
            Description = "Foreign content",
            SubjectId = subjectId,
            GradeId = gradeId,
            TeacherId = teacherId,
            CreatedDate = DateOnly.FromDateTime(DateTime.UtcNow),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
        });
        var activityId = await new ActivityStore(fixture.Database).CreateAsync(new Activity
        {
            Title = searchTerm,
            Description = "Foreign content",
            Date = DateTime.UtcNow.AddDays(1),
            Location = "Foreign campus",
            GradeId = gradeId,
            Capacity = 10,
            CreatedDate = DateTime.UtcNow,
        });
        var materialId = await new LearningMaterialStore(fixture.Database).CreateAsync(new LearningMaterial
        {
            Title = searchTerm,
            Description = "Foreign content",
            SubjectId = subjectId,
            GradeId = gradeId,
            TeacherId = teacherId,
            FileName = "foreign.pdf",
            StoredFileName = $"{suffix}.pdf",
            ContentType = "application/pdf",
            FileSize = 10,
            UploadedDate = DateTime.UtcNow,
        });

        return new ForeignContent(schoolId, gradeId, subjectId, teacherId, homeworkId, activityId, materialId, searchTerm);
    }

    private sealed record ForeignContent(
        int SchoolId,
        int GradeId,
        int SubjectId,
        int TeacherId,
        int HomeworkId,
        int ActivityId,
        int MaterialId,
        string SearchTerm);

    private sealed class TrackingFileStorage : IFileStorage
    {
        public List<string> DeletedFileNames { get; } = [];

        public Task SaveFileAsync(string storedFileName, Stream content, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<Stream?> GetFileAsync(string storedFileName, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream?>(new MemoryStream());

        public Task DeleteFileAsync(string storedFileName, CancellationToken cancellationToken = default)
        {
            DeletedFileNames.Add(storedFileName);
            return Task.CompletedTask;
        }

        public Task<bool> FileExistsAsync(string storedFileName, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }
}