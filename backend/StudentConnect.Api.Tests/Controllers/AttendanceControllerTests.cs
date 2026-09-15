using Microsoft.AspNetCore.Mvc;
using StudentConnect.Api.Controllers;
using StudentConnect.Application;
using StudentConnect.Infrastructure.Data.Stores;
using StudentConnect.Infrastructure.Notifications;
using Xunit;

namespace StudentConnect.Api.Tests.Controllers;

// Runs against a real Postgres test database (see DatabaseFixture).
[Collection("Database")]
public class AttendanceControllerTests(DatabaseFixture fixture)
{
    private AttendanceController CreateController(StudentConnect.Application.ICurrentUserService currentUser) =>
        new(
            new AttendanceStore(fixture.Database),
            new StudentStore(fixture.Database),
            new ReferenceDataStore(fixture.Database),
            new TeacherSubjectAssignmentStore(fixture.Database),
            new NotificationRecipientResolver(new UserStore(fixture.Database), new ParentStore(fixture.Database)),
            currentUser,
            new FakeCurrentTenantService(),
            NullNotificationService.Instance,
            NullAuditService.Instance,
            fixture.Database,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AttendanceController>.Instance);

    // Creates Grade/Section/Subject/Student rows scoped to School 1 ("Demo School", per seed.sql)
    // with GUID-suffixed names so parallel test runs never collide.
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

    [Fact]
    public async Task CreateBulkAttendance_CreatesNewRecords_AndUpdatesExistingInPlace()
    {
        var (subjectId, studentId, _) = await SeedClassAsync();
        var controller = CreateController(TestCurrentUser.Admin());
        var date = DateOnly.FromDateTime(DateTime.UtcNow);

        var firstSave = await controller.CreateBulkAttendance(new BulkAttendanceRequestDto
        {
            SubjectId = subjectId,
            Date = date,
            Records = [new BulkAttendanceItemDto { StudentId = studentId, Status = "Absent" }],
        });
        Assert.IsType<OkObjectResult>(firstSave);

        var recorded = await new AttendanceStore(fixture.Database).GetByStudentAndSubjectAsync(studentId, subjectId);
        var single = Assert.Single(recorded);
        Assert.Equal(StudentConnect.Domain.AttendanceStatus.Absent, single.Status);

        // Re-saving with a changed status must update the same row, not insert a duplicate.
        var secondSave = await controller.CreateBulkAttendance(new BulkAttendanceRequestDto
        {
            SubjectId = subjectId,
            Date = date,
            Records = [new BulkAttendanceItemDto { StudentId = studentId, Status = "Present", Reason = "Corrected" }],
        });
        Assert.IsType<OkObjectResult>(secondSave);

        var afterUpdate = await new AttendanceStore(fixture.Database).GetByStudentAndSubjectAsync(studentId, subjectId);
        var updatedSingle = Assert.Single(afterUpdate);
        Assert.Equal(StudentConnect.Domain.AttendanceStatus.Present, updatedSingle.Status);
    }

    [Fact]
    public async Task CreateBulkAttendance_TeacherOutsideAssignment_ReturnsForbidden()
    {
        var (subjectId, studentId, _) = await SeedClassAsync();
        var teacherId = await SeedTeacherAsync();
        // Deliberately no TeacherSubjectAssignment row created for this teacher/subject/section.
        var controller = CreateController(TestCurrentUser.Teacher(teacherId));

        var result = await controller.CreateBulkAttendance(new BulkAttendanceRequestDto
        {
            SubjectId = subjectId,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Records = [new BulkAttendanceItemDto { StudentId = studentId, Status = "Present" }],
        });

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task CreateBulkAttendance_TeacherWithinAssignment_Succeeds()
    {
        var (subjectId, studentId, sectionId) = await SeedClassAsync();
        var teacherId = await SeedTeacherAsync();
        await new TeacherSubjectAssignmentStore(fixture.Database).CreateAsync(teacherId, subjectId, sectionId);
        var controller = CreateController(TestCurrentUser.Teacher(teacherId));

        var result = await controller.CreateBulkAttendance(new BulkAttendanceRequestDto
        {
            SubjectId = subjectId,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Records = [new BulkAttendanceItemDto { StudentId = studentId, Status = "Present" }],
        });

        Assert.IsType<OkObjectResult>(result);
    }
}
