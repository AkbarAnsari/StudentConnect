using Microsoft.AspNetCore.Http;
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
            new UserStore(fixture.Database),
            currentUser,
            new FakeCurrentTenantService(),
            NullAuditService.Instance,
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
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

    private async Task<(int GradeId, int SectionId, int SubjectId)> SeedEnrollmentPrereqsAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var gradeId = await fixture.Database.ExecuteScalarAsync<int>(
            """INSERT INTO "Grades" ("Name", "SchoolId") VALUES (@name, 1) RETURNING "Id" """,
            p => p.AddWithValue("name", $"Enrollment-{suffix}"));
        var sectionId = await fixture.Database.ExecuteScalarAsync<int>(
            """INSERT INTO "Sections" ("GradeId", "Name") VALUES (@gradeId, 'A') RETURNING "Id" """,
            p => p.AddWithValue("gradeId", gradeId));
        var subjectId = await fixture.Database.ExecuteScalarAsync<int>(
            """INSERT INTO "Subjects" ("Name", "Code", "SchoolId") VALUES (@name, @code, 1) RETURNING "Id" """,
            p =>
            {
                p.AddWithValue("name", $"Enrollment Subject {suffix}");
                p.AddWithValue("code", suffix);
            });
        return (gradeId, sectionId, subjectId);
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

    [Fact]
    public async Task CreateStudent_WithLogin_CreatesEnrollmentAndInvitationAtomically()
    {
        var (gradeId, sectionId, subjectId) = await SeedEnrollmentPrereqsAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"student-{suffix}";

        var result = await CreateController(TestCurrentUser.Admin()).CreateStudent(new StudentCreateDto
        {
            StudentId = $"STU-{suffix}",
            FirstName = "New",
            LastName = "Student",
            Email = $"student-{suffix}@example.com",
            GradeId = gradeId,
            SectionId = sectionId,
            SubjectIds = [subjectId],
            CreateLogin = true,
            Username = username,
        });

        var student = Assert.IsType<StudentResponseDto>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        Assert.Equal([subjectId], await new StudentStore(fixture.Database).GetSubjectIdsForStudentAsync(student.Id));

        var user = await new UserStore(fixture.Database).GetByUsernameForLoginAsync(username);
        Assert.NotNull(user);
        Assert.Equal(student.Id, user.StudentId);
        Assert.Equal(1, await fixture.Database.ExecuteScalarAsync<long>(
            """SELECT COUNT(*) FROM "PasswordResetTokens" WHERE "UserId" = @userId""",
            p => p.AddWithValue("userId", user.Id)));
        Assert.Equal(1, await fixture.Database.ExecuteScalarAsync<long>(
            """SELECT COUNT(*) FROM "EmailOutbox" WHERE "ToUserId" = @userId""",
            p => p.AddWithValue("userId", user.Id)));
    }

    [Fact]
    public async Task CreateStudent_WithForeignSubject_CreatesNothing()
    {
        var (gradeId, sectionId, _) = await SeedEnrollmentPrereqsAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var schoolId = await fixture.Database.ExecuteScalarAsync<int>(
            """INSERT INTO "Schools" ("Name", "Code", "IsActive") VALUES (@name, @code, true) RETURNING "Id" """,
            p =>
            {
                p.AddWithValue("name", $"Foreign {suffix}");
                p.AddWithValue("code", $"foreign-{suffix}");
            });
        var foreignSubjectId = await fixture.Database.ExecuteScalarAsync<int>(
            """INSERT INTO "Subjects" ("Name", "Code", "SchoolId") VALUES (@name, @code, @schoolId) RETURNING "Id" """,
            p =>
            {
                p.AddWithValue("name", $"Foreign Subject {suffix}");
                p.AddWithValue("code", $"F{suffix}");
                p.AddWithValue("schoolId", schoolId);
            });
        var studentCode = $"STU-{suffix}";
        var username = $"student-{suffix}";

        var result = await CreateController(TestCurrentUser.Admin()).CreateStudent(new StudentCreateDto
        {
            StudentId = studentCode,
            FirstName = "Invalid",
            LastName = "Student",
            Email = $"invalid-{suffix}@example.com",
            GradeId = gradeId,
            SectionId = sectionId,
            SubjectIds = [foreignSubjectId],
            CreateLogin = true,
            Username = username,
        });

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.False(await new StudentStore(fixture.Database).StudentCodeExistsAsync(studentCode, 1));
        Assert.False(await new UserStore(fixture.Database).UsernameExistsAsync(username));
    }

    [Fact]
    public async Task ImportStudents_WithUsername_EnrollsSubjectsAndCreatesInvitation()
    {
        var (gradeId, sectionId, _) = await SeedEnrollmentPrereqsAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var gradeName = await fixture.Database.ExecuteScalarAsync<string>(
            """SELECT "Name" FROM "Grades" WHERE "Id" = @id""",
            p => p.AddWithValue("id", gradeId));
        var sectionName = await fixture.Database.ExecuteScalarAsync<string>(
            """SELECT "Name" FROM "Sections" WHERE "Id" = @id""",
            p => p.AddWithValue("id", sectionId));
        var studentCode = $"STU-{suffix}";
        var username = $"student-{suffix}";
        var csv = $"StudentId,FirstName,LastName,Email,GradeName,SectionName,Username\n{studentCode},Imported,Student,import-{suffix}@example.com,{gradeName},{sectionName},{username}";
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));
        var file = new FormFile(stream, 0, stream.Length, "file", "students.csv");

        var result = await CreateController(TestCurrentUser.Admin()).ImportStudents(file);

        var import = Assert.IsType<StudentImportResultDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(1, import.SuccessCount);
        Assert.Empty(import.Errors);
        var student = Assert.Single((await new StudentStore(fixture.Database).GetAllAsync(1)).Where(item => item.StudentId == studentCode));
        Assert.NotEmpty(await new StudentStore(fixture.Database).GetSubjectIdsForStudentAsync(student.Id));
        Assert.NotNull(await new UserStore(fixture.Database).GetByUsernameForLoginAsync(username));
    }
}
