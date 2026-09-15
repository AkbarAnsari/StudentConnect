using Microsoft.AspNetCore.Mvc;
using StudentConnect.Api.Controllers;
using StudentConnect.Application;
using StudentConnect.Infrastructure.Data.Stores;
using Xunit;

namespace StudentConnect.Api.Tests.Controllers;

// Runs against a real Postgres test database (see DatabaseFixture).
[Collection("Database")]
public class ReportsControllerTests(DatabaseFixture fixture)
{
    private ReportsController CreateController(ICurrentUserService currentUser) =>
        new(
            new ReportStore(fixture.Database),
            new GradingBandStore(fixture.Database),
            currentUser,
            new FakeCurrentTenantService());

    private async Task<(int StudentId, int SubjectId)> SeedStudentWithAttendanceAndMarksAsync()
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

        var studentId = await db.ExecuteScalarAsync<int>(
            """
            INSERT INTO "Students" ("StudentId", "FirstName", "LastName", "Email", "GradeId", "SchoolId")
            VALUES (@code, 'Test', 'Student', @email, @gradeId, 1) RETURNING "Id"
            """,
            p =>
            {
                p.AddWithValue("code", $"STU-{suffix}");
                p.AddWithValue("email", $"{suffix}@example.com");
                p.AddWithValue("gradeId", gradeId);
            });

        await db.ExecuteAsync(
            """INSERT INTO "StudentSubjects" ("StudentId", "SubjectId") VALUES (@studentId, @subjectId)""",
            p =>
            {
                p.AddWithValue("studentId", studentId);
                p.AddWithValue("subjectId", subjectId);
            });

        // 3 attendance rows: 2 Present, 1 Absent -> 66.67% (rounded).
        foreach (var status in new[] { "Present", "Present", "Absent" })
        {
            await db.ExecuteAsync(
                """
                INSERT INTO "Attendance" ("StudentId", "SubjectId", "Date", "Status")
                VALUES (@studentId, @subjectId, @date, @status)
                """,
                p =>
                {
                    p.AddWithValue("studentId", studentId);
                    p.AddWithValue("subjectId", subjectId);
                    p.AddWithValue("date", DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-Random.Shared.Next(1, 1000)));
                    p.AddWithValue("status", status);
                });
        }

        var assessmentId = await new AssessmentStore(db).CreateAsync(new StudentConnect.Domain.Assessment
        {
            Title = "Unit Test Assessment",
            SubjectId = subjectId,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            MaximumMarks = 100,
            AssessmentType = StudentConnect.Domain.AssessmentType.UnitTest,
            Description = "",
            Weight = 1.0m,
        });
        await new MarkStore(db).CreateAsync(studentId, assessmentId, 80);

        return (studentId, subjectId);
    }

    [Fact]
    public async Task GetStudentReportCard_InaccessibleStudent_ReturnsForbidden()
    {
        var (studentId, _) = await SeedStudentWithAttendanceAndMarksAsync();
        var controller = CreateController(TestCurrentUser.Student(studentId + 999_999));

        var result = await controller.GetStudentReportCard(studentId);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task GetStudentReportCard_NonexistentStudent_ReturnsNotFound()
    {
        var controller = CreateController(TestCurrentUser.Admin());

        var result = await controller.GetStudentReportCard(999_999);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetStudentReportCard_ComputesAttendanceAndMarksPercentages()
    {
        var (studentId, subjectId) = await SeedStudentWithAttendanceAndMarksAsync();
        var controller = CreateController(TestCurrentUser.Admin());

        var result = await controller.GetStudentReportCard(studentId);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<StudentReportCardDto>(ok.Value);
        var row = Assert.Single(dto.Subjects, r => r.SubjectId == subjectId);
        Assert.Equal(80, dto.OverallMarksPercentage);
        Assert.Equal(66.7, row.AttendancePercentage, precision: 1);
    }

    [Fact]
    public async Task GetStudentTranscript_InaccessibleStudent_ReturnsForbidden()
    {
        var (studentId, _) = await SeedStudentWithAttendanceAndMarksAsync();
        var controller = CreateController(TestCurrentUser.Student(studentId + 999_999));

        var result = await controller.GetStudentTranscript(studentId);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task GetTeacherClassReports_NonOwningTeacher_ReturnsForbidden()
    {
        var controller = CreateController(TestCurrentUser.Teacher(1));

        var result = await controller.GetTeacherClassReports(999_999);

        Assert.IsType<ForbidResult>(result.Result);
    }
}
