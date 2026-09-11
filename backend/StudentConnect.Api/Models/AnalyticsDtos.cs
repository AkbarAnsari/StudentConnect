namespace StudentConnect.Api.Models;

public class AnalyticsOverviewDto
{
    public int TotalStudents { get; set; }
    public int TotalTeachers { get; set; }
    public int TotalSubjects { get; set; }
    public double OverallAttendancePercentage { get; set; }
    public double AverageMarksPercentage { get; set; }
}

public class GradeAttendanceDto
{
    public int GradeId { get; set; }
    public string GradeName { get; set; } = string.Empty;
    public int TotalRecords { get; set; }
    public double AttendancePercentage { get; set; }
}

public class GradeMarksDto
{
    public int GradeId { get; set; }
    public string GradeName { get; set; } = string.Empty;
    public double AverageMarksPercentage { get; set; }
    public string ResultCategory { get; set; } = string.Empty;
}

public class AtRiskStudentDto
{
    public int Id { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string GradeName { get; set; } = string.Empty;
    public double? AttendancePercentage { get; set; }
    public double? AverageMarksPercentage { get; set; }
}
