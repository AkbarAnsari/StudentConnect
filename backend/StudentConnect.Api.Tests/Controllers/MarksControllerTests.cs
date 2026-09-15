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
public class MarksControllerTests(DatabaseFixture fixture)
{
    private MarksController CreateController(ICurrentUserService currentUser) =>
        new(
            new MarkStore(fixture.Database),
            new StudentStore(fixture.Database),
            new AssessmentStore(fixture.Database),
            new TermStore(fixture.Database),
            new TeacherSubjectAssignmentStore(fixture.Database),
            new NotificationRecipientResolver(new UserStore(fixture.Database), new ParentStore(fixture.Database)),
            currentUser,
            new FakeCurrentTenantService(),
            new GradingBandStore(fixture.Database),
            NullNotificationService.Instance,
            NullAuditService.Instance,
            fixture.Database,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<MarksController>.Instance);

    private async Task<(int SubjectId, int StudentId, int SectionId)> SeedClassAsync()
    {
        var db = fixture.Database;
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var gradeId = await db.ExecuteScalarAsync<int>(
            """INSERT INTO "Grades" ("Name", "SchoolId") VALUES (@name, 1) RETURNING "Id" """,
            p => p.AddWithValue("name", $"Grade-{suffix}"));

        var sectionId = await db.ExecuteScalarAsync<int>(
            """INSERT INTO "Sections" ("GradeId", "Name") VALUES (@gradeId, 'A') RETURNING "Id" """,
            p => p.AddWithValue("gradeId", gradeId));

        var subjectId = await db.ExecuteScalarAsync<int>(
            """INSERT INTO "Subjects" ("Name", "Code", "SchoolId") VALUES (@name, @code, 1) RETURNING "Id" """,
            p =>
            {
                p.AddWithValue("name", $"Subject-{suffix}");
                p.AddWithValue("code", suffix);
            });

        var studentId = await db.ExecuteScalarAsync<int>(
            """
            INSERT INTO "Students" ("StudentId", "FirstName", "LastName", "Email", "GradeId", "SectionId", "SchoolId")
            VALUES (@code, 'Test', 'Student', @email, @gradeId, @sectionId, 1) RETURNING "Id"
            """,
            p =>
            {
                p.AddWithValue("code", $"STU-{suffix}");
                p.AddWithValue("email", $"{suffix}@example.com");
                p.AddWithValue("gradeId", gradeId);
                p.AddWithValue("sectionId", sectionId);
            });

        return (subjectId, studentId, sectionId);
    }

    private async Task<int> SeedTeacherAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        return await fixture.Database.ExecuteScalarAsync<int>(
            """
            INSERT INTO "Teachers" ("TeacherId", "FirstName", "LastName", "Email", "SchoolId")
            VALUES (@code, 'Test', 'Teacher', @email, 1) RETURNING "Id"
            """,
            p =>
            {
                p.AddWithValue("code", $"TCH-{suffix}");
                p.AddWithValue("email", $"teacher-{suffix}@example.com");
            });
    }

    private async Task<int> SeedAssessmentAsync(int subjectId, int maximumMarks, decimal weight) =>
        await new AssessmentStore(fixture.Database).CreateAsync(new Assessment
        {
            Title = "Test Assessment",
            SubjectId = subjectId,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            MaximumMarks = maximumMarks,
            AssessmentType = AssessmentType.UnitTest,
            Description = "",
            Weight = weight,
        });

    [Fact]
    public async Task CreateBulkMarks_CreatesNewRecords_AndUpdatesExistingInPlace()
    {
        var (subjectId, studentId, _) = await SeedClassAsync();
        var assessmentId = await SeedAssessmentAsync(subjectId, maximumMarks: 100, weight: 1.0m);
        var controller = CreateController(TestCurrentUser.Admin());

        var firstSave = await controller.CreateBulkMarks(new BulkMarksRequestDto
        {
            AssessmentId = assessmentId,
            Marks = [new BulkMarkItemDto { StudentId = studentId, MarksObtained = 70 }],
        });
        Assert.IsType<OkObjectResult>(firstSave);

        var recorded = await new MarkStore(fixture.Database).GetByStudentAndSubjectAsync(studentId, subjectId);
        Assert.Equal(70, Assert.Single(recorded).MarksObtained);

        var secondSave = await controller.CreateBulkMarks(new BulkMarksRequestDto
        {
            AssessmentId = assessmentId,
            Marks = [new BulkMarkItemDto { StudentId = studentId, MarksObtained = 85 }],
        });
        Assert.IsType<OkObjectResult>(secondSave);

        var afterUpdate = await new MarkStore(fixture.Database).GetByStudentAndSubjectAsync(studentId, subjectId);
        Assert.Equal(85, Assert.Single(afterUpdate).MarksObtained);
    }

    [Fact]
    public async Task GetStudentMarksSummary_ComputesWeightedAverage()
    {
        var (subjectId, studentId, _) = await SeedClassAsync();
        // 50/100 (weight 1) and 100/100 (weight 3) -> weighted = (0.5*1 + 1.0*3) / 4 = 87.5% -> rounds to 88.
        var lightAssessmentId = await SeedAssessmentAsync(subjectId, maximumMarks: 100, weight: 1.0m);
        var heavyAssessmentId = await SeedAssessmentAsync(subjectId, maximumMarks: 100, weight: 3.0m);
        var marks = new MarkStore(fixture.Database);
        await marks.CreateAsync(studentId, lightAssessmentId, 50);
        await marks.CreateAsync(studentId, heavyAssessmentId, 100);

        var controller = CreateController(TestCurrentUser.Admin());
        var result = await controller.GetStudentMarksSummary(studentId);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var summary = Assert.Single((IEnumerable<MarksSummaryDto>)ok.Value!);
        Assert.Equal(88, summary.Percentage);
    }

    [Fact]
    public async Task CreateBulkMarks_TeacherOutsideAssignment_ReturnsForbidden()
    {
        var (subjectId, studentId, _) = await SeedClassAsync();
        var assessmentId = await SeedAssessmentAsync(subjectId, maximumMarks: 100, weight: 1.0m);
        var teacherId = await SeedTeacherAsync();
        var controller = CreateController(TestCurrentUser.Teacher(teacherId));

        var result = await controller.CreateBulkMarks(new BulkMarksRequestDto
        {
            AssessmentId = assessmentId,
            Marks = [new BulkMarkItemDto { StudentId = studentId, MarksObtained = 70 }],
        });

        Assert.IsType<ForbidResult>(result);
    }
}
