using StudentConnect.Domain;

namespace StudentConnect.Application;

// One subject's row within a student's report card.
public class SubjectReportRowDto
{
    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public double AttendancePercentage { get; set; }
    public double MarksPercentage { get; set; }
    public string ResultCategory { get; set; } = string.Empty;
}

// A single student's consolidated report card — attendance, marks, rank, and homework
// completion pulled together in one view instead of five separate pages.
public class StudentReportCardDto
{
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string GradeName { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public double OverallAttendancePercentage { get; set; }
    public double OverallMarksPercentage { get; set; }
    public string OverallResultCategory { get; set; } = string.Empty;
    public int RankInGrade { get; set; }
    public int TotalStudentsInGrade { get; set; }
    public double HomeworkCompletionRate { get; set; }
    public List<SubjectReportRowDto> Subjects { get; set; } = [];
    public int? PeriodId { get; set; }
    public string PeriodLabel { get; set; } = "All recorded activity";
    public DateOnly? PeriodStartDate { get; set; }
    public DateOnly? PeriodEndDate { get; set; }
    public string ReportStatus { get; set; } = "Draft";
    public int? IssuanceId { get; set; }
    public DateTime? IssuedAtUtc { get; set; }
    public string? Comments { get; set; }
    public string? TemplateName { get; set; }
    public string? HeaderText { get; set; }
    public string? FooterText { get; set; }
    public List<string> SignatureLabels { get; set; } = [];
    public DateTime GeneratedAtUtc { get; set; }
}

public class ReportPeriodDto
{
    public int Id { get; set; }
    public int AcademicYearId { get; set; }
    public string AcademicYearName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsLocked { get; set; }
    public bool IsCurrent { get; set; }
    public string Label => $"{AcademicYearName} - {Name}";
}

public class ReportIssueRequestDto
{
    public int TermId { get; set; }
    public string? Comments { get; set; }
    public int? TemplateId { get; set; }
}

public class ReportIssuanceSummaryDto
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public int TermId { get; set; }
    public string PeriodLabel { get; set; } = string.Empty;
    public DateOnly PeriodStartDate { get; set; }
    public DateOnly PeriodEndDate { get; set; }
    public string? Comments { get; set; }
    public int IssuedByUserId { get; set; }
    public string IssuedByName { get; set; } = string.Empty;
    public DateTime IssuedAtUtc { get; set; }
    public string? TemplateName { get; set; }
    public int SignatureCount { get; set; }
    public DateTime? LastDownloadedAtUtc { get; set; }
}

public class ReportTemplateDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string HeaderText { get; set; } = string.Empty;
    public string FooterText { get; set; } = string.Empty;
    public string PrimarySignatureLabel { get; set; } = "Principal";
    public string? SecondarySignatureLabel { get; set; }
    public bool IsDefault { get; set; }
}

public class ReportTemplateSaveDto : ReportTemplateDto { }

public class ReportSignatureCreateDto
{
    public string Label { get; set; } = string.Empty;
    public string SignedName { get; set; } = string.Empty;
}

public class ReportSignatureDto
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string SignedName { get; set; } = string.Empty;
    public string SignedByName { get; set; } = string.Empty;
    public DateTime SignedAtUtc { get; set; }
}

public class ReportBatchIssueDto
{
    public int TermId { get; set; }
    public int? GradeId { get; set; }
    public int? TemplateId { get; set; }
    public string? Comments { get; set; }
}

public class ReportBatchResultDto
{
    public int Issued { get; set; }
    public int Skipped { get; set; }
}

public class TranscriptIssuanceSummaryDto
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public string IssuedByName { get; set; } = string.Empty;
    public DateTime IssuedAtUtc { get; set; }
}

// Performance rollup for one subject and section assignment.
public class SectionClassReportDto
{
    public int AssignmentId { get; set; }
    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public int SectionId { get; set; }
    public string GradeName { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public int StudentCount { get; set; }
    public double AverageAttendancePercentage { get; set; }
    public double AverageMarksPercentage { get; set; }
    public int AtRiskCount { get; set; }
}

// One subject's marks within a single academic year, for the multi-year transcript.
public class TranscriptSubjectRowDto
{
    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public double MarksPercentage { get; set; }
    public string ResultCategory { get; set; } = string.Empty;
}

// One academic year's row within a student's multi-year transcript.
public class TranscriptYearRowDto
{
    public int AcademicYearId { get; set; }
    public string AcademicYearName { get; set; } = string.Empty;
    public double OverallMarksPercentage { get; set; }
    public string OverallResultCategory { get; set; } = string.Empty;
    public List<TranscriptSubjectRowDto> Subjects { get; set; } = [];
}

// A student's full academic history — every year that has any recorded Marks, oldest first.
public class StudentTranscriptDto
{
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public List<TranscriptYearRowDto> Years { get; set; } = [];
}
