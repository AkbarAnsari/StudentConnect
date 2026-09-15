using Microsoft.AspNetCore.Mvc;
using StudentConnect.Api.Controllers;
using StudentConnect.Application;
using StudentConnect.Infrastructure.Data.Stores;
using Xunit;

namespace StudentConnect.Api.Tests.Controllers;

// Runs against a real Postgres test database (see DatabaseFixture).
[Collection("Database")]
public class StudentsControllerTests(DatabaseFixture fixture)
{
    private StudentsController CreateController(ICurrentUserService currentUser) =>
        new(
            new StudentStore(fixture.Database),
            new ReferenceDataStore(fixture.Database),
            new SectionStore(fixture.Database),
            currentUser,
            new FakeCurrentTenantService(),
            NullAuditService.Instance,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<StudentsController>.Instance);

    private async Task<(int StudentId, int LinkedSubjectId, int UnlinkedSubjectId)> SeedStudentWithOneLinkedSubjectAsync()
    {
        var db = fixture.Database;
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var gradeId = await db.ExecuteScalarAsync<int>(
            """INSERT INTO "Grades" ("Name", "SchoolId") VALUES (@name, 1) RETURNING "Id" """,
            p => p.AddWithValue("name", $"Grade-{suffix}"));

        var linkedSubjectId = await db.ExecuteScalarAsync<int>(
            """INSERT INTO "Subjects" ("Name", "Code", "SchoolId") VALUES (@name, @code, 1) RETURNING "Id" """,
            p =>
            {
                p.AddWithValue("name", $"Linked-{suffix}");
                p.AddWithValue("code", $"L{suffix}");
            });

        var unlinkedSubjectId = await db.ExecuteScalarAsync<int>(
            """INSERT INTO "Subjects" ("Name", "Code", "SchoolId") VALUES (@name, @code, 1) RETURNING "Id" """,
            p =>
            {
                p.AddWithValue("name", $"Unlinked-{suffix}");
                p.AddWithValue("code", $"U{suffix}");
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
                p.AddWithValue("subjectId", linkedSubjectId);
            });

        return (studentId, linkedSubjectId, unlinkedSubjectId);
    }

    [Fact]
    public async Task GetStudentSubjects_ReturnsOnlyLinkedSubjects()
    {
        var (studentId, linkedSubjectId, unlinkedSubjectId) = await SeedStudentWithOneLinkedSubjectAsync();
        var controller = CreateController(TestCurrentUser.Admin());

        var result = await controller.GetStudentSubjects(studentId);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var subjects = Assert.IsAssignableFrom<IEnumerable<StudentSubjectDto>>(ok.Value).ToList();

        Assert.Single(subjects);
        Assert.Equal(linkedSubjectId, subjects[0].Id);
        Assert.DoesNotContain(subjects, s => s.Id == unlinkedSubjectId);
    }

    [Fact]
    public async Task GetStudentSubjects_UnlinkedStudent_ReturnsEmptyNotHardcodedList()
    {
        var db = fixture.Database;
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var gradeId = await db.ExecuteScalarAsync<int>(
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
                p.AddWithValue("gradeId", gradeId);
            });

        var controller = CreateController(TestCurrentUser.Admin());
        var result = await controller.GetStudentSubjects(studentId);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<StudentSubjectDto>>(ok.Value));
    }
}
