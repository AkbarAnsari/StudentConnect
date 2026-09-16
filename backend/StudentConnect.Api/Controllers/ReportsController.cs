using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Data.Stores;
using StudentConnect.Infrastructure.Notifications;

namespace StudentConnect.Api.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController(
    ReportStore reports,
    GradingBandStore gradingBands,
    ICurrentUserService currentUser,
    ICurrentTenantService currentTenant,
    ReportIssuanceStore? issuances = null,
    StudentStore? students = null,
    TeacherSubjectAssignmentStore? assignments = null,
    NotificationRecipientResolver? recipients = null,
    INotificationService? notifications = null,
    IAuditService? audit = null,
    ReportTemplateStore? templates = null,
    ReportGovernanceStore? governance = null) : ControllerBase
{
    [HttpGet("periods")]
    public async Task<ActionResult<IEnumerable<ReportPeriodDto>>> GetReportPeriods()
    {
        if (currentTenant.SchoolId is not { } schoolId) return Forbid();
        if (issuances is null) return Ok(Array.Empty<ReportPeriodDto>());
        return Ok(await issuances.GetPeriodsAsync(schoolId));
    }

    // GET /api/reports/student/{studentId}/report-card
    [HttpGet("student/{studentId:int}/report-card")]
    public async Task<ActionResult<StudentReportCardDto>> GetStudentReportCard(int studentId, [FromQuery] int? termId = null, [FromQuery] int? issuanceId = null)
    {
        var (report, error) = await LoadReportCardAsync(studentId, termId, issuanceId);
        return error is null ? Ok(report) : new ActionResult<StudentReportCardDto>(error);
    }

    [HttpGet("student/{studentId:int}/report-card/history")]
    public async Task<ActionResult<IEnumerable<ReportIssuanceSummaryDto>>> GetReportCardHistory(int studentId)
    {
        if (currentTenant.SchoolId is not { } schoolId || !await CanAccessReportStudentAsync(studentId, schoolId)) return Forbid();
        return Ok(issuances is null ? [] : await issuances.GetHistoryAsync(studentId, schoolId));
    }

    [HttpPost("student/{studentId:int}/report-card/issue")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<StudentReportCardDto>> IssueStudentReportCard(int studentId, ReportIssueRequestDto dto)
    {
        if (currentTenant.SchoolId is not { } schoolId || currentUser.UserId is not { } userId) return Forbid();
        if (!await CanAccessReportStudentAsync(studentId, schoolId)) return Forbid();
        if (issuances is null) return StatusCode(500, new { message = "Report issuance is unavailable." });
        if (await issuances.GetPeriodAsync(dto.TermId, schoolId) is not { } period)
            return BadRequest(new { message = "Reporting period not found." });
        if (dto.Comments?.Length > 2000) return BadRequest(new { message = "Comments cannot exceed 2000 characters." });

        var report = await reports.GetStudentReportCardAsync(studentId, schoolId, period.StartDate, period.EndDate);
        if (report is null) return NotFound();
        SetPeriod(report, period);
        await ApplyGradingBandsAsync(report);
        report.ReportStatus = "Issued";
        report.IssuedAtUtc = DateTime.UtcNow;
        report.Comments = string.IsNullOrWhiteSpace(dto.Comments) ? null : dto.Comments.Trim();
        var template = templates is null ? null : dto.TemplateId is { } templateId
            ? await templates.GetByIdAsync(templateId, schoolId)
            : (await templates.GetAllAsync(schoolId)).FirstOrDefault(item => item.IsDefault);
        if (dto.TemplateId is not null && template is null) return BadRequest(new { message = "Report template not found." });
        ApplyTemplate(report, template);
        var issuanceId = await issuances.CreateAsync(
            schoolId, studentId, period, JsonSerializer.Serialize(report), report.Comments, userId, template?.Id);
        report.IssuanceId = issuanceId;

        if (recipients is not null && notifications is not null)
        {
            var userIds = await recipients.ForStudentAsync(studentId);
            if (userIds.Count > 0)
                await notifications.NotifyManyTrackedAsync(userIds, $"{period.Label} report issued", "A finalized report card is available to review.", NotificationType.General, $"/students/{studentId}/report-card?termId={period.Id}", "Important", null);
        }
        if (audit is not null)
            await audit.LogAsync("Issue", "ReportCard", issuanceId.ToString(), $"StudentId={studentId}, Period={period.Label}");
        return CreatedAtAction(nameof(GetStudentReportCard), new { studentId, issuanceId = report.IssuanceId }, report);
    }

    [HttpPost("report-cards/batch")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ReportBatchResultDto>> IssueBatch(ReportBatchIssueDto dto)
    {
        if (currentTenant.SchoolId is not { } schoolId || currentUser.UserId is not { } userId || students is null || issuances is null) return Forbid();
        var period = await issuances.GetPeriodAsync(dto.TermId, schoolId);
        if (period is null) return BadRequest(new { message = "Reporting period not found." });
        var template = templates is null ? null : dto.TemplateId is { } templateId ? await templates.GetByIdAsync(templateId, schoolId) : (await templates.GetAllAsync(schoolId)).FirstOrDefault(item => item.IsDefault);
        if (dto.TemplateId is not null && template is null) return BadRequest(new { message = "Report template not found." });
        var roster = (await students.GetAllAsync(schoolId, StudentStatus.Active)).Where(student => dto.GradeId is null || student.GradeId == dto.GradeId).ToList();
        var result = new ReportBatchResultDto();
        foreach (var student in roster)
        {
            var report = await reports.GetStudentReportCardAsync(student.Id, schoolId, period.StartDate, period.EndDate);
            if (report is null) { result.Skipped++; continue; }
            SetPeriod(report, period); await ApplyGradingBandsAsync(report); ApplyTemplate(report, template);
            report.ReportStatus="Issued";report.IssuedAtUtc=DateTime.UtcNow;report.Comments=string.IsNullOrWhiteSpace(dto.Comments)?null:dto.Comments.Trim();
            await issuances.CreateAsync(schoolId,student.Id,period,JsonSerializer.Serialize(report),report.Comments,userId,template?.Id);
            result.Issued++;
        }
        if(audit is not null)await audit.LogAsync("BatchIssue","ReportCard",dto.TermId.ToString(),$"Issued={result.Issued};Skipped={result.Skipped};GradeId={dto.GradeId}");
        return Ok(result);
    }

    [HttpPut("issuances/{issuanceId:int}/sign")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Sign(int issuanceId, ReportSignatureCreateDto dto)
    {
        if(currentTenant.SchoolId is not{} schoolId||currentUser.UserId is not{} userId||governance is null)return Forbid();
        if(string.IsNullOrWhiteSpace(dto.Label)||string.IsNullOrWhiteSpace(dto.SignedName))return BadRequest(new{message="Signature label and signed name are required."});
        await governance.AddSignatureAsync(issuanceId,schoolId,dto.Label.Trim(),dto.SignedName.Trim(),userId);
        if(audit is not null)await audit.LogAsync("Sign","ReportIssuance",issuanceId.ToString(),dto.Label);
        return NoContent();
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

        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        var classReports = await reports.GetTeacherClassReportsAsync(teacherId, schoolId);
        return Ok(classReports);
    }

    // GET /api/reports/student/{studentId}/transcript — every academic year with recorded
    // Marks, oldest first, for a full multi-year academic history in one view.
    [HttpGet("student/{studentId:int}/transcript")]
    public async Task<ActionResult<StudentTranscriptDto>> GetStudentTranscript(int studentId, [FromQuery] int? issuanceId = null)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        if (!await CanAccessReportStudentAsync(studentId, schoolId)) return Forbid();

        StudentTranscriptDto? transcript;
        if(issuanceId is { } historicalId && governance is not null)
        {
            var snapshot=await governance.GetTranscriptSnapshotAsync(historicalId,studentId,schoolId);
            transcript=snapshot is null?null:JsonSerializer.Deserialize<StudentTranscriptDto>(snapshot);
        }
        else transcript = await reports.GetStudentTranscriptAsync(studentId, schoolId);
        if (transcript is null)
        {
            return NotFound();
        }

        await ApplyGradingBandsToTranscriptAsync(transcript, schoolId);
        return Ok(transcript);
    }

    [HttpPost("student/{studentId:int}/transcript/issue")]
    [Authorize(Roles="Teacher,Admin")]
    public async Task<ActionResult<TranscriptIssuanceSummaryDto>> IssueTranscript(int studentId)
    {
        if(currentTenant.SchoolId is not{} schoolId||currentUser.UserId is not{} userId||governance is null)return Forbid();
        if(!await CanAccessReportStudentAsync(studentId,schoolId))return Forbid();
        var transcript=await reports.GetStudentTranscriptAsync(studentId,schoolId);if(transcript is null)return NotFound();
        await ApplyGradingBandsToTranscriptAsync(transcript,schoolId);
        var id=await governance.CreateTranscriptAsync(schoolId,studentId,JsonSerializer.Serialize(transcript),userId);
        if(audit is not null)await audit.LogAsync("Issue","Transcript",id.ToString(),$"StudentId={studentId}");
        return Ok((await governance.GetTranscriptHistoryAsync(studentId,schoolId)).First(item=>item.Id==id));
    }

    [HttpGet("student/{studentId:int}/transcript/history")]
    public async Task<ActionResult<IEnumerable<TranscriptIssuanceSummaryDto>>> GetTranscriptHistory(int studentId)
    {
        if(currentTenant.SchoolId is not{} schoolId||governance is null)return Forbid();
        if(!await CanAccessReportStudentAsync(studentId,schoolId))return Forbid();
        return Ok(await governance.GetTranscriptHistoryAsync(studentId,schoolId));
    }

    // GET /api/reports/student/{studentId}/transcript/export
    [HttpGet("student/{studentId:int}/transcript/export")]
    public async Task<IActionResult> ExportStudentTranscript(int studentId)
    {
        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        if (!await CanAccessReportStudentAsync(studentId, schoolId)) return Forbid();

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
    public async Task<IActionResult> ExportStudentReportCard(int studentId, [FromQuery] int? termId = null, [FromQuery] int? issuanceId = null)
    {
        var (reportCard, error) = await LoadReportCardAsync(studentId, termId, issuanceId);
        if (error is not null) return error;
        if (issuanceId is { } downloadedIssuanceId && governance is not null && currentTenant.SchoolId is { } schoolId && currentUser.UserId is { } userId)
            await governance.RecordDownloadAsync(downloadedIssuanceId, schoolId, userId);
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

        if (currentTenant.SchoolId is not { } schoolId)
        {
            return Forbid();
        }

        var classReports = await reports.GetTeacherClassReportsAsync(teacherId, schoolId);
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
        sb.AppendLine($"Period,{Csv(r.PeriodLabel)}");
        sb.AppendLine($"Status,{Csv(r.ReportStatus)}");
        sb.AppendLine($"Issued At UTC,{r.IssuedAtUtc:O}");
        sb.AppendLine($"Comments,{Csv(r.Comments ?? string.Empty)}");
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
        sb.AppendLine("Subject,Grade,Section,Student Count,Avg Attendance %,Avg Marks %,At Risk Count");
        foreach (var row in rows)
        {
            sb.AppendLine($"{Csv(row.SubjectName)},{Csv(row.GradeName)},{Csv(row.SectionName)},{row.StudentCount},{row.AverageAttendancePercentage},{row.AverageMarksPercentage},{row.AtRiskCount}");
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

    private async Task<(StudentReportCardDto Report, ActionResult? Error)> LoadReportCardAsync(int studentId, int? termId, int? issuanceId)
    {
        if (currentTenant.SchoolId is not { } schoolId || !await CanAccessReportStudentAsync(studentId, currentTenant.SchoolId ?? 0))
            return (new(), Forbid());

        if (issuances is not null && (issuanceId is not null || currentUser.Role is "Student" or "Parent"))
        {
            var issued = issuanceId is { } id
                ? await issuances.GetByIdAsync(id, studentId, schoolId)
                : await issuances.GetLatestAsync(studentId, termId, schoolId);
            if (issued is null)
                return (new(), NotFound(new { message = "No issued report is available for this period." }));
            var snapshot = JsonSerializer.Deserialize<StudentReportCardDto>(issued.SnapshotJson);
            if (snapshot is null) return (new(), StatusCode(500, new { message = "The issued report snapshot is invalid." }));
            ApplyIssuance(snapshot, issued.Summary);
            return (snapshot, null);
        }

        ReportPeriodDto? period = null;
        if (issuances is not null)
        {
            period = termId is { } id ? await issuances.GetPeriodAsync(id, schoolId) : await issuances.GetCurrentPeriodAsync(schoolId);
            if (termId is not null && period is null)
                return (new(), BadRequest(new { message = "Reporting period not found." }));
        }
        var report = await reports.GetStudentReportCardAsync(studentId, schoolId, period?.StartDate, period?.EndDate);
        if (report is null) return (new(), NotFound());
        if (period is not null) SetPeriod(report, period);
        await ApplyGradingBandsAsync(report);
        return (report, null);
    }

    private async Task<bool> CanAccessReportStudentAsync(int studentId, int schoolId)
    {
        if (!currentUser.CanAccessStudent(studentId)) return false;
        if (currentUser.Role != "Teacher" || students is null || assignments is null) return true;
        var student = await students.GetByIdAsync(studentId, schoolId);
        return student?.SectionId is { } sectionId && currentUser.TeacherId is { } teacherId &&
            await assignments.TeacherHasSectionAsync(teacherId, sectionId, schoolId);
    }

    private static void SetPeriod(StudentReportCardDto report, ReportPeriodDto period)
    {
        report.PeriodId = period.Id;
        report.PeriodLabel = period.Label;
        report.PeriodStartDate = period.StartDate;
        report.PeriodEndDate = period.EndDate;
    }

    private static void ApplyTemplate(StudentReportCardDto report, ReportTemplateDto? template)
    {
        if(template is null)return;
        report.TemplateName=template.Name;report.HeaderText=template.HeaderText;report.FooterText=template.FooterText;
        report.SignatureLabels=[template.PrimarySignatureLabel];
        if(!string.IsNullOrWhiteSpace(template.SecondarySignatureLabel))report.SignatureLabels.Add(template.SecondarySignatureLabel);
    }

    private static void ApplyIssuance(StudentReportCardDto report, ReportIssuanceSummaryDto issuance)
    {
        report.ReportStatus = "Issued";
        report.IssuanceId = issuance.Id;
        report.PeriodId = issuance.TermId;
        report.PeriodLabel = issuance.PeriodLabel;
        report.PeriodStartDate = issuance.PeriodStartDate;
        report.PeriodEndDate = issuance.PeriodEndDate;
        report.IssuedAtUtc = issuance.IssuedAtUtc;
        report.Comments = issuance.Comments;
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
