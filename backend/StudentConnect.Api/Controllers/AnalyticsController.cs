using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
public class AnalyticsController(AnalyticsStore analytics, GradingBandStore gradingBands, ICurrentTenantService currentTenant) : ControllerBase
{
    // GET /api/analytics/overview
    [HttpGet("overview")]
    public async Task<ActionResult<AnalyticsOverviewDto>> GetOverview()
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

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

        var rows = await analytics.GetMarksByGradeAsync(schoolId);
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

        return Ok(await analytics.GetAtRiskStudentsAsync(schoolId, attendanceThreshold, marksThreshold));
    }
}
