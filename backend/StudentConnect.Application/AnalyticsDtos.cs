using StudentConnect.Domain;

namespace StudentConnect.Application;

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
    public string SectionName { get; set; } = string.Empty;
    public double? AttendancePercentage { get; set; }
    public double? AverageMarksPercentage { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public List<string> Reasons { get; set; } = [];
}

// One calendar month's rollup, for the analytics trend-over-time chart.
public class MonthlyTrendDto
{
    public string Month { get; set; } = string.Empty;
    public double AttendancePercentage { get; set; }
    public double MarksPercentage { get; set; }
}

public class AnalyticsDashboardDto
{
    public AnalyticsOverviewDto Overview { get; set; } = new();
    public List<GradeAttendanceDto> AttendanceByGrade { get; set; } = [];
    public List<GradeMarksDto> MarksByGrade { get; set; } = [];
    public List<AtRiskStudentDto> AtRiskStudents { get; set; } = [];
    public List<MonthlyTrendDto> Trend { get; set; } = [];
    public string ScopeLabel { get; set; } = string.Empty;
    public DateOnly DateFrom { get; set; }
    public DateOnly DateTo { get; set; }
    public double AttendanceThreshold { get; set; }
    public double MarksThreshold { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
}
