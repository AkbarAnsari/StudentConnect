using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentConnect.Api.Data;
using StudentConnect.Api.Models;

namespace StudentConnect.Api.Controllers;

// Read-only aggregate views over existing data — no new tables, just Teacher/Admin-facing
// summaries of Attendance/Marks that already exist. Numbers are computed on demand rather
// than cached, which is fine at this data volume (a real deployment would revisit that).
[ApiController]
[Route("api/analytics")]
[Authorize(Roles = "Teacher,Admin")]
public class AnalyticsController(StudentConnectDbContext db) : ControllerBase
{
    // GET /api/analytics/overview
    [HttpGet("overview")]
    public async Task<ActionResult<AnalyticsOverviewDto>> GetOverview()
    {
        var totalStudents = await db.Students.CountAsync();
        var totalTeachers = await db.Teachers.CountAsync();
        var totalSubjects = await db.Subjects.CountAsync();

        var attendanceTotals = await db.Attendance
            .GroupBy(a => 1)
            .Select(g => new { Total = g.Count(), Present = g.Count(a => a.Status == AttendanceStatus.Present) })
            .FirstOrDefaultAsync();

        var overallAttendancePercentage = attendanceTotals is { Total: > 0 }
            ? Math.Round(attendanceTotals.Present * 100.0 / attendanceTotals.Total, 1)
            : 0;

        var marks = await db.Marks
            .Include(m => m.Assessment)
            .Select(m => new { m.MarksObtained, Max = m.Assessment!.MaximumMarks })
            .ToListAsync();
        var averageMarksPercentage = marks.Count > 0
            ? Math.Round(marks.Average(m => m.MarksObtained * 100.0 / m.Max), 1)
            : 0;

        return Ok(new AnalyticsOverviewDto
        {
            TotalStudents = totalStudents,
            TotalTeachers = totalTeachers,
            TotalSubjects = totalSubjects,
            OverallAttendancePercentage = overallAttendancePercentage,
            AverageMarksPercentage = averageMarksPercentage,
        });
    }

    // GET /api/analytics/attendance-by-grade
    [HttpGet("attendance-by-grade")]
    public async Task<ActionResult<IEnumerable<GradeAttendanceDto>>> GetAttendanceByGrade()
    {
        var byGrade = await db.Attendance
            .Include(a => a.Student)
            .GroupBy(a => a.Student!.GradeId)
            .Select(g => new { GradeId = g.Key, Total = g.Count(), Present = g.Count(a => a.Status == AttendanceStatus.Present) })
            .ToListAsync();

        var grades = await db.Grades.AsNoTracking().ToDictionaryAsync(g => g.Id, g => g.Name);

        var result = byGrade.Select(d => new GradeAttendanceDto
        {
            GradeId = d.GradeId,
            GradeName = grades.GetValueOrDefault(d.GradeId, "Unknown"),
            TotalRecords = d.Total,
            AttendancePercentage = d.Total > 0 ? Math.Round(d.Present * 100.0 / d.Total, 1) : 0,
        }).OrderBy(r => r.GradeName).ToList();

        return Ok(result);
    }

    // GET /api/analytics/marks-by-grade
    [HttpGet("marks-by-grade")]
    public async Task<ActionResult<IEnumerable<GradeMarksDto>>> GetMarksByGrade()
    {
        var marks = await db.Marks
            .Include(m => m.Student)
            .Include(m => m.Assessment)
            .Select(m => new { GradeId = m.Student!.GradeId, Percentage = m.MarksObtained * 100.0 / m.Assessment!.MaximumMarks })
            .ToListAsync();

        var grades = await db.Grades.AsNoTracking().ToDictionaryAsync(g => g.Id, g => g.Name);

        var result = marks
            .GroupBy(m => m.GradeId)
            .Select(g =>
            {
                var average = Math.Round(g.Average(x => x.Percentage), 1);
                return new GradeMarksDto
                {
                    GradeId = g.Key,
                    GradeName = grades.GetValueOrDefault(g.Key, "Unknown"),
                    AverageMarksPercentage = average,
                    ResultCategory = ResultCategory.From(average),
                };
            })
            .OrderBy(r => r.GradeName)
            .ToList();

        return Ok(result);
    }

    // GET /api/analytics/at-risk-students — flags a student when EITHER their attendance
    // or their average marks fall below the given threshold (defaults are demo-reasonable,
    // not derived from any real policy).
    [HttpGet("at-risk-students")]
    public async Task<ActionResult<IEnumerable<AtRiskStudentDto>>> GetAtRiskStudents(
        [FromQuery] double attendanceThreshold = 75,
        [FromQuery] double marksThreshold = 40)
    {
        var students = await db.Students.Include(s => s.Grade).AsNoTracking().ToListAsync();

        var attendanceByStudent = await db.Attendance
            .GroupBy(a => a.StudentId)
            .Select(g => new { StudentId = g.Key, Total = g.Count(), Present = g.Count(a => a.Status == AttendanceStatus.Present) })
            .ToDictionaryAsync(x => x.StudentId);

        var marksByStudent = await db.Marks
            .Include(m => m.Assessment)
            .Select(m => new { m.StudentId, Percentage = m.MarksObtained * 100.0 / m.Assessment!.MaximumMarks })
            .ToListAsync();
        var marksGrouped = marksByStudent.GroupBy(m => m.StudentId).ToDictionary(g => g.Key, g => g.Average(x => x.Percentage));

        var result = new List<AtRiskStudentDto>();
        foreach (var student in students)
        {
            double? attendancePct = attendanceByStudent.TryGetValue(student.Id, out var att) && att.Total > 0
                ? Math.Round(att.Present * 100.0 / att.Total, 1)
                : null;

            double? marksPct = marksGrouped.TryGetValue(student.Id, out var avg) ? Math.Round(avg, 1) : null;

            var isAtRisk = (attendancePct is not null && attendancePct < attendanceThreshold) ||
                           (marksPct is not null && marksPct < marksThreshold);

            if (isAtRisk)
            {
                result.Add(new AtRiskStudentDto
                {
                    Id = student.Id,
                    StudentId = student.StudentId,
                    FullName = $"{student.FirstName} {student.LastName}",
                    GradeName = student.Grade?.Name ?? string.Empty,
                    AttendancePercentage = attendancePct,
                    AverageMarksPercentage = marksPct,
                });
            }
        }

        return Ok(result.OrderBy(r => r.AttendancePercentage ?? 100).ToList());
    }
}
