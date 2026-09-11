using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentConnect.Api.Controllers;
using StudentConnect.Api.Data;
using StudentConnect.Api.Models;
using Xunit;

namespace StudentConnect.Api.Tests.Controllers;

// Uses the real StudentConnectDbContext (with its HasData seed) against the EF Core
// InMemory provider, then independently recomputes the same aggregates from the seeded
// data to check the controller's math — rather than hardcoding counts that would break
// every time the seed data changes.
public class AnalyticsControllerTests
{
    private static StudentConnectDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<StudentConnectDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new StudentConnectDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public async Task GetOverview_MatchesIndependentlyComputedAggregates()
    {
        await using var db = CreateDb();

        var expectedStudents = await db.Students.CountAsync();
        var expectedTeachers = await db.Teachers.CountAsync();
        var expectedSubjects = await db.Subjects.CountAsync();

        var attendance = await db.Attendance.ToListAsync();
        var expectedAttendancePct = attendance.Count > 0
            ? Math.Round(attendance.Count(a => a.Status == AttendanceStatus.Present) * 100.0 / attendance.Count, 1)
            : 0;

        var marks = await db.Marks.Include(m => m.Assessment).ToListAsync();
        var expectedMarksPct = marks.Count > 0
            ? Math.Round(marks.Average(m => m.MarksObtained * 100.0 / m.Assessment!.MaximumMarks), 1)
            : 0;

        var controller = new AnalyticsController(db);
        var result = await controller.GetOverview();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<AnalyticsOverviewDto>(ok.Value);

        Assert.Equal(expectedStudents, dto.TotalStudents);
        Assert.Equal(expectedTeachers, dto.TotalTeachers);
        Assert.Equal(expectedSubjects, dto.TotalSubjects);
        Assert.Equal(expectedAttendancePct, dto.OverallAttendancePercentage);
        Assert.Equal(expectedMarksPct, dto.AverageMarksPercentage);
    }

    [Fact]
    public async Task GetAtRiskStudents_FlagsAStudentWithZeroPercentAttendance()
    {
        await using var db = CreateDb();

        var grade = await db.Grades.FirstAsync();
        var subject = await db.Subjects.FirstAsync();

        var student = new Student
        {
            Id = 9001,
            StudentId = "STUX-ATRISK",
            FirstName = "AtRisk",
            LastName = "Tester",
            Email = "atrisk@example.com",
            GradeId = grade.Id,
            Section = "Z",
        };
        db.Students.Add(student);
        db.Attendance.Add(new Attendance
        {
            StudentId = student.Id,
            SubjectId = subject.Id,
            Date = new DateOnly(2026, 1, 1),
            Status = AttendanceStatus.Absent,
        });
        await db.SaveChangesAsync();

        var controller = new AnalyticsController(db);
        var result = await controller.GetAtRiskStudents();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var atRisk = Assert.IsAssignableFrom<IEnumerable<AtRiskStudentDto>>(ok.Value);

        Assert.Contains(atRisk, s => s.Id == student.Id && s.AttendancePercentage == 0);
    }
}
