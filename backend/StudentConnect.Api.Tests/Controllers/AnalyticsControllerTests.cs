using Microsoft.AspNetCore.Mvc;
using StudentConnect.Api.Controllers;
using StudentConnect.Api.Data.Stores;
using StudentConnect.Api.Models;
using Xunit;

namespace StudentConnect.Api.Tests.Controllers;

// Runs against a real Postgres test database (see DatabaseFixture) — there's no more EF Core
// InMemory provider to substitute now that the API talks to Postgres directly via ADO.NET.
[Collection("Database")]
public class AnalyticsControllerTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task GetOverview_MatchesIndependentlyComputedAggregates()
    {
        var db = fixture.Database;

        var expectedStudents = await db.ExecuteScalarAsync<long>("""SELECT COUNT(*) FROM "Students" """, bind: null);
        var expectedTeachers = await db.ExecuteScalarAsync<long>("""SELECT COUNT(*) FROM "Teachers" """, bind: null);
        var expectedSubjects = await db.ExecuteScalarAsync<long>("""SELECT COUNT(*) FROM "Subjects" """, bind: null);
        var attendanceTotal = await db.ExecuteScalarAsync<long>("""SELECT COUNT(*) FROM "Attendance" """, bind: null);
        var attendancePresent = await db.ExecuteScalarAsync<long>(
            """SELECT COUNT(*) FROM "Attendance" WHERE "Status" = 'Present'""", bind: null);
        var marksCount = await db.ExecuteScalarAsync<long>("""SELECT COUNT(*) FROM "Marks" """, bind: null);
        var marksSum = await db.ExecuteScalarAsync<double>(
            """
            SELECT COALESCE(SUM(m."MarksObtained" * 100.0 / a."MaximumMarks"), 0)::double precision
            FROM "Marks" m JOIN "Assessments" a ON a."Id" = m."AssessmentId"
            """,
            bind: null);

        var expectedAttendancePct = attendanceTotal > 0 ? Math.Round(attendancePresent * 100.0 / attendanceTotal, 1) : 0;
        var expectedMarksPct = marksCount > 0 ? Math.Round(marksSum / marksCount, 1) : 0;

        var controller = new AnalyticsController(new AnalyticsStore(db));
        var result = await controller.GetOverview();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<AnalyticsOverviewDto>(ok.Value);

        Assert.Equal((int)expectedStudents, dto.TotalStudents);
        Assert.Equal((int)expectedTeachers, dto.TotalTeachers);
        Assert.Equal((int)expectedSubjects, dto.TotalSubjects);
        Assert.Equal(expectedAttendancePct, dto.OverallAttendancePercentage);
        Assert.Equal(expectedMarksPct, dto.AverageMarksPercentage);
    }

    [Fact]
    public async Task GetAtRiskStudents_FlagsAStudentWithZeroPercentAttendance()
    {
        var db = fixture.Database;

        var gradeId = await db.ExecuteScalarAsync<int>("""SELECT "Id" FROM "Grades" ORDER BY "Id" LIMIT 1""", bind: null);
        var subjectId = await db.ExecuteScalarAsync<int>("""SELECT "Id" FROM "Subjects" ORDER BY "Id" LIMIT 1""", bind: null);

        var uniqueCode = $"ATRISK{Guid.NewGuid():N}"[..15];
        var studentId = await db.ExecuteScalarAsync<int>(
            """
            INSERT INTO "Students" ("StudentId", "FirstName", "LastName", "Email", "Section", "GradeId")
            VALUES (@code, 'AtRisk', 'Tester', @email, 'Z', @gradeId)
            RETURNING "Id"
            """,
            p =>
            {
                p.AddWithValue("code", uniqueCode);
                p.AddWithValue("email", $"{uniqueCode}@example.com");
                p.AddWithValue("gradeId", gradeId);
            });

        await db.ExecuteAsync(
            """INSERT INTO "Attendance" ("StudentId", "SubjectId", "Date", "Status") VALUES (@studentId, @subjectId, @date, 'Absent')""",
            p =>
            {
                p.AddWithValue("studentId", studentId);
                p.AddWithValue("subjectId", subjectId);
                p.AddWithValue("date", new DateOnly(2026, 1, 1));
            });

        var controller = new AnalyticsController(new AnalyticsStore(db));
        var result = await controller.GetAtRiskStudents();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var atRisk = Assert.IsAssignableFrom<IEnumerable<AtRiskStudentDto>>(ok.Value);

        Assert.Contains(atRisk, s => s.Id == studentId && s.AttendancePercentage == 0);
    }
}
