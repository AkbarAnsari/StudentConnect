namespace StudentConnect.Api.Models;

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
}

// Performance rollup for one section a teacher is homeroom teacher of.
public class SectionClassReportDto
{
    public int SectionId { get; set; }
    public string GradeName { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public int StudentCount { get; set; }
    public double AverageAttendancePercentage { get; set; }
    public double AverageMarksPercentage { get; set; }
    public int AtRiskCount { get; set; }
}
