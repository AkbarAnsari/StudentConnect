using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentConnect.Api.Auth;
using StudentConnect.Api.Data.Stores;
using StudentConnect.Api.Models;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController(ReportStore reports, ICurrentUserService currentUser) : ControllerBase
{
    // GET /api/reports/student/{studentId}/report-card
    [HttpGet("student/{studentId:int}/report-card")]
    public async Task<ActionResult<StudentReportCardDto>> GetStudentReportCard(int studentId)
    {
        if (!currentUser.CanAccessStudent(studentId))
        {
            return Forbid();
        }

        var reportCard = await reports.GetStudentReportCardAsync(studentId);
        if (reportCard is null)
        {
            return NotFound();
        }

        return Ok(reportCard);
    }

    // GET /api/reports/teacher/{teacherId}/classes
    [HttpGet("teacher/{teacherId:int}/classes")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<IEnumerable<SectionClassReportDto>>> GetTeacherClassReports(int teacherId)
    {
        if (!currentUser.CanActAsTeacher(teacherId))
        {
            return Forbid();
        }

        var classReports = await reports.GetTeacherClassReportsAsync(teacherId);
        return Ok(classReports);
    }
}
