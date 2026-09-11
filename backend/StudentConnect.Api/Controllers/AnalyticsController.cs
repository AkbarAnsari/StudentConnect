using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentConnect.Api.Data.Stores;
using StudentConnect.Api.Models;

namespace StudentConnect.Api.Controllers;

// Read-only aggregate views over existing data — no new tables, just Teacher/Admin-facing
// summaries of Attendance/Marks that already exist. Numbers are computed in SQL (see
// AnalyticsStore) rather than cached, which is fine at this data volume.
[ApiController]
[Route("api/analytics")]
[Authorize(Roles = "Teacher,Admin")]
public class AnalyticsController(AnalyticsStore analytics) : ControllerBase
{
    // GET /api/analytics/overview
    [HttpGet("overview")]
    public async Task<ActionResult<AnalyticsOverviewDto>> GetOverview() =>
        Ok(await analytics.GetOverviewAsync());

    // GET /api/analytics/attendance-by-grade
    [HttpGet("attendance-by-grade")]
    public async Task<ActionResult<IEnumerable<GradeAttendanceDto>>> GetAttendanceByGrade() =>
        Ok(await analytics.GetAttendanceByGradeAsync());

    // GET /api/analytics/marks-by-grade
    [HttpGet("marks-by-grade")]
    public async Task<ActionResult<IEnumerable<GradeMarksDto>>> GetMarksByGrade() =>
        Ok(await analytics.GetMarksByGradeAsync());

    // GET /api/analytics/at-risk-students — flags a student when EITHER their attendance
    // or their average marks fall below the given threshold (defaults are demo-reasonable,
    // not derived from any real policy).
    [HttpGet("at-risk-students")]
    public async Task<ActionResult<IEnumerable<AtRiskStudentDto>>> GetAtRiskStudents(
        [FromQuery] double attendanceThreshold = 75,
        [FromQuery] double marksThreshold = 40) =>
        Ok(await analytics.GetAtRiskStudentsAsync(attendanceThreshold, marksThreshold));
}
