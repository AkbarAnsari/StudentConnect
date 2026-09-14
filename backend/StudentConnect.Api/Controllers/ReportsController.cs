using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Data.Stores;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController(ReportStore reports, GradingBandStore gradingBands, ICurrentUserService currentUser, ICurrentTenantService currentTenant) : ControllerBase
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

        await ApplyGradingBandsAsync(reportCard);
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

    // GET /api/reports/student/{studentId}/transcript — every academic year with recorded
    // Marks, oldest first, for a full multi-year academic history in one view.
    [HttpGet("student/{studentId:int}/transcript")]
    public async Task<ActionResult<StudentTranscriptDto>> GetStudentTranscript(int studentId)
    {
        if (!currentUser.CanAccessStudent(studentId))
        {
            return Forbid();
        }

        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        var transcript = await reports.GetStudentTranscriptAsync(studentId, schoolId);
        if (transcript is null)
        {
            return NotFound();
        }

        await ApplyGradingBandsToTranscriptAsync(transcript, schoolId);
        return Ok(transcript);
    }

    // GET /api/reports/student/{studentId}/transcript/export
    [HttpGet("student/{studentId:int}/transcript/export")]
    public async Task<IActionResult> ExportStudentTranscript(int studentId)
    {
        if (!currentUser.CanAccessStudent(studentId))
        {
            return Forbid();
        }

        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        var transcript = await reports.GetStudentTranscriptAsync(studentId, schoolId);
        if (transcript is null)
        {
            return NotFound();
        }

        await ApplyGradingBandsToTranscriptAsync(transcript, schoolId);
        var csv = BuildTranscriptCsv(transcript);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"transcript-{transcript.StudentCode}.csv");
    }

    // GET /api/reports/student/{studentId}/report-card/export
    [HttpGet("student/{studentId:int}/report-card/export")]
    public async Task<IActionResult> ExportStudentReportCard(int studentId)
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

        await ApplyGradingBandsAsync(reportCard);
        var csv = BuildReportCardCsv(reportCard);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"report-card-{reportCard.StudentCode}.csv");
    }

    // GET /api/reports/teacher/{teacherId}/classes/export
    [HttpGet("teacher/{teacherId:int}/classes/export")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<IActionResult> ExportTeacherClassReports(int teacherId)
    {
        if (!currentUser.CanActAsTeacher(teacherId))
        {
            return Forbid();
        }

        var classReports = await reports.GetTeacherClassReportsAsync(teacherId);
        var csv = BuildClassReportsCsv(classReports);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", "class-reports.csv");
    }

    private static string BuildReportCardCsv(StudentReportCardDto r)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Field,Value");
        sb.AppendLine($"Student Code,{Csv(r.StudentCode)}");
        sb.AppendLine($"Full Name,{Csv(r.FullName)}");
        sb.AppendLine($"Grade,{Csv(r.GradeName)}");
        sb.AppendLine($"Section,{Csv(r.SectionName)}");
        sb.AppendLine($"Overall Attendance %,{r.OverallAttendancePercentage}");
        sb.AppendLine($"Overall Marks %,{r.OverallMarksPercentage}");
        sb.AppendLine($"Overall Result,{Csv(r.OverallResultCategory)}");
        sb.AppendLine($"Rank In Grade,{r.RankInGrade} of {r.TotalStudentsInGrade}");
        sb.AppendLine($"Homework Completion %,{r.HomeworkCompletionRate}");
        sb.AppendLine();
        sb.AppendLine("Subject,Attendance %,Marks %,Result");
        foreach (var s in r.Subjects)
        {
            sb.AppendLine($"{Csv(s.SubjectName)},{s.AttendancePercentage},{s.MarksPercentage},{Csv(s.ResultCategory)}");
        }
        return sb.ToString();
    }

    private static string BuildClassReportsCsv(IEnumerable<SectionClassReportDto> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Grade,Section,Student Count,Avg Attendance %,Avg Marks %,At Risk Count");
        foreach (var row in rows)
        {
            sb.AppendLine($"{Csv(row.GradeName)},{Csv(row.SectionName)},{row.StudentCount},{row.AverageAttendancePercentage},{row.AverageMarksPercentage},{row.AtRiskCount}");
        }
        return sb.ToString();
    }

    private static string BuildTranscriptCsv(StudentTranscriptDto transcript)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Student Code,{Csv(transcript.StudentCode)}");
        sb.AppendLine($"Full Name,{Csv(transcript.FullName)}");
        sb.AppendLine();
        sb.AppendLine("Academic Year,Subject,Marks %,Result");
        foreach (var year in transcript.Years)
        {
            foreach (var subject in year.Subjects)
            {
                sb.AppendLine($"{Csv(year.AcademicYearName)},{Csv(subject.SubjectName)},{subject.MarksPercentage},{Csv(subject.ResultCategory)}");
            }
            sb.AppendLine($"{Csv(year.AcademicYearName)},Overall,{year.OverallMarksPercentage},{Csv(year.OverallResultCategory)}");
        }
        return sb.ToString();
    }

    // Minimal CSV escaping — quotes a value only if it contains a comma or a quote.
    private static string Csv(string value) => value.Contains(',') || value.Contains('"')
        ? $"\"{value.Replace("\"", "\"\"")}\""
        : value;

    private async Task ApplyGradingBandsAsync(StudentReportCardDto reportCard)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return;
        }

        var bands = await gradingBands.GetForSchoolAsync(schoolId);
        reportCard.OverallResultCategory = GradingBandStore.Resolve(bands, reportCard.OverallMarksPercentage);
        foreach (var subject in reportCard.Subjects)
        {
            subject.ResultCategory = GradingBandStore.Resolve(bands, subject.MarksPercentage);
        }
    }

    private async Task ApplyGradingBandsToTranscriptAsync(StudentTranscriptDto transcript, int schoolId)
    {
        var bands = await gradingBands.GetForSchoolAsync(schoolId);
        foreach (var year in transcript.Years)
        {
            year.OverallResultCategory = GradingBandStore.Resolve(bands, year.OverallMarksPercentage);
            foreach (var subject in year.Subjects)
            {
                subject.ResultCategory = GradingBandStore.Resolve(bands, subject.MarksPercentage);
            }
        }
    }
}
