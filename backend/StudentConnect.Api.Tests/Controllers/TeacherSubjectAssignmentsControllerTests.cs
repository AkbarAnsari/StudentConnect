using Microsoft.AspNetCore.Mvc;
using StudentConnect.Api.Controllers;
using StudentConnect.Application;
using StudentConnect.Infrastructure.Data.Stores;
using Xunit;

namespace StudentConnect.Api.Tests.Controllers;

// Runs against a real Postgres test database (see DatabaseFixture).
[Collection("Database")]
public class TeacherSubjectAssignmentsControllerTests(DatabaseFixture fixture)
{
    private TeacherSubjectAssignmentsController CreateController(ICurrentUserService currentUser) =>
        new(
            new TeacherSubjectAssignmentStore(fixture.Database),
            new TeacherStore(fixture.Database),
            new SectionStore(fixture.Database),
            new ReferenceDataStore(fixture.Database),
            new StudentStore(fixture.Database),
            currentUser,
            new FakeCurrentTenantService(),
            NullAuditService.Instance);

    private async Task<(int TeacherId, int SubjectId, int SectionId)> SeedPrereqsAsync()
    {
        var db = fixture.Database;
        var suffix = Guid.NewGuid().ToString("N")[..8];

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

        var subjectId = await db.ExecuteScalarAsync<int>(
            """INSERT INTO "Subjects" ("Name", "Code", "SchoolId") VALUES (@name, @code, 1) RETURNING "Id" """,
            p =>
            {
                p.AddWithValue("name", $"Subject-{suffix}");
                p.AddWithValue("code", suffix);
            });

        var gradeId = await db.ExecuteScalarAsync<int>(
            """INSERT INTO "Grades" ("Name", "SchoolId") VALUES (@name, 1) RETURNING "Id" """,
            p => p.AddWithValue("name", $"Grade-{suffix}"));

        var sectionId = await db.ExecuteScalarAsync<int>(
            """INSERT INTO "Sections" ("GradeId", "Name") VALUES (@gradeId, 'A') RETURNING "Id" """,
            p => p.AddWithValue("gradeId", gradeId));

        return (teacherId, subjectId, sectionId);
    }

    [Fact]
    public async Task Create_ValidAssignment_Succeeds()
    {
        var (teacherId, subjectId, sectionId) = await SeedPrereqsAsync();
        var controller = CreateController(TestCurrentUser.Admin());

        var result = await controller.Create(new TeacherSubjectAssignmentCreateDto { TeacherId = teacherId, SubjectId = subjectId, SectionId = sectionId });

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var dto = Assert.IsType<TeacherSubjectAssignmentDto>(created.Value);
        Assert.Equal(teacherId, dto.TeacherId);
    }

    [Fact]
    public async Task Create_DuplicateAssignment_ReturnsConflict()
    {
        var (teacherId, subjectId, sectionId) = await SeedPrereqsAsync();
        var controller = CreateController(TestCurrentUser.Admin());
        await controller.Create(new TeacherSubjectAssignmentCreateDto { TeacherId = teacherId, SubjectId = subjectId, SectionId = sectionId });

        var result = await controller.Create(new TeacherSubjectAssignmentCreateDto { TeacherId = teacherId, SubjectId = subjectId, SectionId = sectionId });

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetMine_ReturnsOnlyCallersAssignments()
    {
        var (teacherId, subjectId, sectionId) = await SeedPrereqsAsync();
        var (otherTeacherId, otherSubjectId, otherSectionId) = await SeedPrereqsAsync();
        var admin = CreateController(TestCurrentUser.Admin());
        await admin.Create(new TeacherSubjectAssignmentCreateDto { TeacherId = teacherId, SubjectId = subjectId, SectionId = sectionId });
        await admin.Create(new TeacherSubjectAssignmentCreateDto { TeacherId = otherTeacherId, SubjectId = otherSubjectId, SectionId = otherSectionId });

        var result = await CreateController(TestCurrentUser.Teacher(teacherId)).GetMine();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var mine = Assert.IsAssignableFrom<IEnumerable<TeacherSubjectAssignmentDto>>(ok.Value).ToList();
        Assert.Single(mine);
        Assert.Equal(teacherId, mine[0].TeacherId);
    }

    [Fact]
    public async Task GetStudents_ReturnsOnlyActiveStudentsEnrolledInAssignmentsSubject()
    {
        var (teacherId, subjectId, sectionId) = await SeedPrereqsAsync();
        var otherSubjectId = await fixture.Database.ExecuteScalarAsync<int>(
            """INSERT INTO "Subjects" ("Name", "Code", "SchoolId") VALUES (@name, @code, 1) RETURNING "Id" """,
            p =>
            {
                var suffix = Guid.NewGuid().ToString("N")[..8];
                p.AddWithValue("name", $"Other-{suffix}");
                p.AddWithValue("code", suffix);
            });

        async Task<int> InsertStudentAsync(string status)
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            return await fixture.Database.ExecuteScalarAsync<int>(
                """
                INSERT INTO "Students" ("StudentId", "FirstName", "LastName", "Email", "GradeId", "SectionId", "SchoolId", "Status")
                SELECT @code, 'Roster', 'Student', @email, "GradeId", @sectionId, 1, @status
                FROM "Sections" WHERE "Id" = @sectionId
                RETURNING "Id"
                """,
                p =>
                {
                    p.AddWithValue("code", $"STU-{suffix}");
                    p.AddWithValue("email", $"student-{suffix}@example.com");
                    p.AddWithValue("sectionId", sectionId);
                    p.AddWithValue("status", status);
                });
        }

        var activeEnrolledId = await InsertStudentAsync("Active");
        var activeUnenrolledId = await InsertStudentAsync("Active");
        var inactiveEnrolledId = await InsertStudentAsync("Withdrawn");
        await fixture.Database.ExecuteAsync(
            """
            INSERT INTO "StudentSubjects" ("StudentId", "SubjectId")
            VALUES (@activeEnrolledId, @subjectId), (@activeUnenrolledId, @otherSubjectId), (@inactiveEnrolledId, @subjectId)
            """,
            p =>
            {
                p.AddWithValue("activeEnrolledId", activeEnrolledId);
                p.AddWithValue("activeUnenrolledId", activeUnenrolledId);
                p.AddWithValue("inactiveEnrolledId", inactiveEnrolledId);
                p.AddWithValue("subjectId", subjectId);
                p.AddWithValue("otherSubjectId", otherSubjectId);
            });

        var admin = CreateController(TestCurrentUser.Admin());
        var created = await admin.Create(new TeacherSubjectAssignmentCreateDto { TeacherId = teacherId, SubjectId = subjectId, SectionId = sectionId });
        var assignment = Assert.IsType<TeacherSubjectAssignmentDto>(Assert.IsType<CreatedAtActionResult>(created.Result).Value);

        var result = await CreateController(TestCurrentUser.Teacher(teacherId)).GetStudents(assignment.Id);

        var roster = Assert.IsAssignableFrom<IEnumerable<SectionStudentDto>>(Assert.IsType<OkObjectResult>(result.Result).Value).ToList();
        Assert.Equal(activeEnrolledId, Assert.Single(roster).Id);
    }

    [Fact]
    public async Task Delete_RemovesAssignment()
    {
        var (teacherId, subjectId, sectionId) = await SeedPrereqsAsync();
        var admin = CreateController(TestCurrentUser.Admin());
        var createResult = await admin.Create(new TeacherSubjectAssignmentCreateDto { TeacherId = teacherId, SubjectId = subjectId, SectionId = sectionId });
        var created = Assert.IsType<CreatedAtActionResult>(createResult.Result);
        var dto = Assert.IsType<TeacherSubjectAssignmentDto>(created.Value);

        var deleteResult = await admin.Delete(dto.Id);

        Assert.IsType<NoContentResult>(deleteResult);
        Assert.Null(await new TeacherSubjectAssignmentStore(fixture.Database).GetByIdAsync(dto.Id));
    }
}
