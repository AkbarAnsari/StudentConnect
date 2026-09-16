using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Data.Stores;

namespace StudentConnect.Api.Controllers;

// Read-only aggregate views over existing data — no new tables, just Teacher/Admin-facing
// summaries of Attendance/Marks that already exist. Numbers are computed in SQL (see
// AnalyticsStore) rather than cached, which is fine at this data volume.
[ApiController]
[Route("api/analytics")]
[Authorize(Roles = "Teacher,Admin")]
public class AnalyticsController(
    AnalyticsStore analytics,
    GradingBandStore gradingBands,
    ICurrentTenantService currentTenant,
    AnalyticsDashboardStore? dashboardAnalytics = null,
    TeacherSubjectAssignmentStore? assignments = null,
    ICurrentUserService? currentUser = null) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<AnalyticsDashboardDto>> GetDashboard(
        [FromQuery] int? assignmentId,
        [FromQuery] DateOnly? dateFrom,
        [FromQuery] DateOnly? dateTo,
        [FromQuery] double attendanceThreshold = 75,
        [FromQuery] double marksThreshold = 40)
    {
        var (data, error) = await LoadDashboardAsync(assignmentId, dateFrom, dateTo, attendanceThreshold, marksThreshold);
        return error ?? Ok(data);
    }

    [HttpGet("dashboard/export")]
    public async Task<IActionResult> ExportDashboard(
        [FromQuery] int? assignmentId,
        [FromQuery] DateOnly? dateFrom,
        [FromQuery] DateOnly? dateTo,
        [FromQuery] double attendanceThreshold = 75,
        [FromQuery] double marksThreshold = 40)
    {
        var (data, error) = await LoadDashboardAsync(assignmentId, dateFrom, dateTo, attendanceThreshold, marksThreshold);
        if (error is not null) return error;

        var csv = BuildDashboardCsv(data!);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"analytics-{data!.DateFrom:yyyyMMdd}-{data.DateTo:yyyyMMdd}.csv");
    }

    // GET /api/analytics/overview
    [HttpGet("overview")]
    public async Task<ActionResult<AnalyticsOverviewDto>> GetOverview()
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        if (currentUser?.Role == "Teacher" && await LoadLegacyTeacherDashboardAsync() is { } dashboard)
            return Ok(dashboard.Overview);
        return Ok(await analytics.GetOverviewAsync(schoolId));
    }

    // GET /api/analytics/trend?months=12 — monthly attendance/marks rollup, oldest first.
    [HttpGet("trend")]
    public async Task<ActionResult<IEnumerable<MonthlyTrendDto>>> GetTrend([FromQuery] int months = 12)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        var bounded = Math.Clamp(months, 1, 36);
        if (currentUser?.Role == "Teacher" && await LoadLegacyTeacherDashboardAsync(bounded) is { } dashboard)
            return Ok(dashboard.Trend);
        return Ok(await analytics.GetTrendAsync(schoolId, bounded));
    }

    // GET /api/analytics/attendance-by-grade
    [HttpGet("attendance-by-grade")]
    public async Task<ActionResult<IEnumerable<GradeAttendanceDto>>> GetAttendanceByGrade()
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        if (currentUser?.Role == "Teacher" && await LoadLegacyTeacherDashboardAsync() is { } dashboard)
            return Ok(dashboard.AttendanceByGrade);
        return Ok(await analytics.GetAttendanceByGradeAsync(schoolId));
    }

    // GET /api/analytics/marks-by-grade
    [HttpGet("marks-by-grade")]
    public async Task<ActionResult<IEnumerable<GradeMarksDto>>> GetMarksByGrade()
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        var rows = currentUser?.Role == "Teacher" && await LoadLegacyTeacherDashboardAsync() is { } dashboard
            ? dashboard.MarksByGrade
            : await analytics.GetMarksByGradeAsync(schoolId);
        var bands = await gradingBands.GetForSchoolAsync(schoolId);
        foreach (var row in rows)
        {
            row.ResultCategory = GradingBandStore.Resolve(bands, row.AverageMarksPercentage);
        }
        return Ok(rows);
    }

    // GET /api/analytics/at-risk-students — flags a student when EITHER their attendance
    // or their average marks fall below the given threshold (defaults are demo-reasonable,
    // not derived from any real policy).
    [HttpGet("at-risk-students")]
    public async Task<ActionResult<IEnumerable<AtRiskStudentDto>>> GetAtRiskStudents(
        [FromQuery] double attendanceThreshold = 75,
        [FromQuery] double marksThreshold = 40)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        if (currentUser?.Role == "Teacher" && dashboardAnalytics is not null && currentUser.TeacherId is { } teacherId)
        {
            var filter = new AnalyticsDashboardFilter(schoolId, teacherId, null, DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-3)), DateOnly.FromDateTime(DateTime.UtcNow), attendanceThreshold, marksThreshold);
            return Ok((await dashboardAnalytics.GetDashboardAsync(filter)).AtRiskStudents);
        }
        return Ok(await analytics.GetAtRiskStudentsAsync(schoolId, attendanceThreshold, marksThreshold));
    }

    private async Task<(AnalyticsDashboardDto? Data, ActionResult? Error)> LoadDashboardAsync(
        int? assignmentId,
        DateOnly? requestedFrom,
        DateOnly? requestedTo,
        double attendanceThreshold,
        double marksThreshold)
    {
        if (currentTenant.SchoolId is not { } schoolId) return (null, Forbid());
        if (dashboardAnalytics is null || assignments is null || currentUser is null)
            return (null, StatusCode(500, new { message = "Analytics dashboard is unavailable." }));
        if (attendanceThreshold is < 0 or > 100 || marksThreshold is < 0 or > 100)
            return (null, BadRequest(new { message = "Thresholds must be between 0 and 100." }));

        var dateTo = requestedTo ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var dateFrom = requestedFrom ?? dateTo.AddMonths(-3);
        if (dateFrom > dateTo || dateFrom < dateTo.AddYears(-3))
            return (null, BadRequest(new { message = "Choose a valid date range of no more than three years." }));

        var teacherId = currentUser.Role == "Teacher" ? currentUser.TeacherId : null;
        if (currentUser.Role == "Teacher" && teacherId is null) return (null, Forbid());

        TeacherSubjectAssignment? assignment = null;
        if (assignmentId is { } selectedAssignmentId)
        {
            assignment = await assignments.GetByIdAsync(selectedAssignmentId, schoolId);
            if (assignment is null) return (null, BadRequest(new { message = "Class assignment not found." }));
            if (teacherId is not null && assignment.TeacherId != teacherId) return (null, Forbid());
        }

        var filter = new AnalyticsDashboardFilter(schoolId, teacherId, assignmentId, dateFrom, dateTo, attendanceThreshold, marksThreshold);
        var dashboard = await dashboardAnalytics.GetDashboardAsync(filter);
        var bands = await gradingBands.GetForSchoolAsync(schoolId);
        foreach (var row in dashboard.MarksByGrade)
            row.ResultCategory = GradingBandStore.Resolve(bands, row.AverageMarksPercentage);
        dashboard.ScopeLabel = assignment is not null
            ? $"{assignment.Section?.Grade?.Name} {assignment.Section?.Name} - {assignment.Subject?.Name}"
            : currentUser.Role == "Teacher" ? "My assigned classes" : "Whole school";
        return (dashboard, null);
    }

    private async Task<AnalyticsDashboardDto?> LoadLegacyTeacherDashboardAsync(int months = 36)
    {
        if (currentTenant.SchoolId is not { } schoolId || currentUser?.TeacherId is not { } teacherId || dashboardAnalytics is null)
            return null;
        var dateTo = DateOnly.FromDateTime(DateTime.UtcNow);
        return await dashboardAnalytics.GetDashboardAsync(new AnalyticsDashboardFilter(
            schoolId, teacherId, null, dateTo.AddMonths(-Math.Clamp(months, 1, 36)), dateTo, 75, 40));
    }

    private static string BuildDashboardCsv(AnalyticsDashboardDto dashboard)
    {
        var csv = new StringBuilder();
        csv.AppendLine($"Scope,{Csv(dashboard.ScopeLabel)}");
        csv.AppendLine($"Date range,{dashboard.DateFrom:yyyy-MM-dd} to {dashboard.DateTo:yyyy-MM-dd}");
        csv.AppendLine($"Active students,{dashboard.Overview.TotalStudents}");
        csv.AppendLine($"Attendance %,{dashboard.Overview.OverallAttendancePercentage}");
        csv.AppendLine($"Published marks %,{dashboard.Overview.AverageMarksPercentage}");
        csv.AppendLine();
        csv.AppendLine("Student,Student ID,Grade,Section,Attendance %,Published marks %,Risk level,Risk reasons");
        foreach (var student in dashboard.AtRiskStudents)
            csv.AppendLine($"{Csv(student.FullName)},{Csv(student.StudentId)},{Csv(student.GradeName)},{Csv(student.SectionName)},{student.AttendancePercentage},{student.AverageMarksPercentage},{student.RiskLevel},{Csv(string.Join("; ", student.Reasons))}");
        return csv.ToString();
    }

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}
