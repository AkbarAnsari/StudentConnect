namespace StudentConnect.Application;

public class StudentImportRowResult
{
    public int Row { get; set; }
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? StudentId { get; set; }
}

// Only failed rows are reported in Errors — successful rows are just counted, not detailed
// out, to keep the response small for a large import.
public class StudentImportResultDto
{
    public int TotalRows { get; set; }
    public int SuccessCount { get; set; }
    public List<StudentImportRowResult> Errors { get; set; } = [];
}
