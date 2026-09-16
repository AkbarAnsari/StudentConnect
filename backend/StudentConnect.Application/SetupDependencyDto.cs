namespace StudentConnect.Application;

public class SetupDependencyDto
{
    public string ResourceType { get; set; } = string.Empty;
    public int ResourceId { get; set; }
    public int StudentCount { get; set; }
    public int SectionCount { get; set; }
    public int AssessmentCount { get; set; }
    public int ContentCount { get; set; }
    public int AssignmentCount { get; set; }
    public int IssuedReportCount { get; set; }
    public int TotalCount => StudentCount + SectionCount + AssessmentCount + ContentCount + AssignmentCount + IssuedReportCount;
}