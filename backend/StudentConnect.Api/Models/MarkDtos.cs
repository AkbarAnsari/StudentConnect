namespace StudentConnect.Api.Models;

// What Angular receives for one student's mark — combines Assessment + Subject info.
public class MarkRecordDto
{
    public int Id { get; set; }
    public int AssessmentId { get; set; }
    public string AssessmentTitle { get; set; } = string.Empty;
    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public DateOnly AssessmentDate { get; set; }
    public int MarksObtained { get; set; }
    public int MaximumMarks { get; set; }

    public static MarkRecordDto FromEntity(Mark mark) => new()
    {
        Id = mark.Id,
        AssessmentId = mark.AssessmentId,
        AssessmentTitle = mark.Assessment?.Title ?? string.Empty,
        SubjectId = mark.Assessment?.SubjectId ?? 0,
        SubjectName = mark.Assessment?.Subject?.Name ?? string.Empty,
        AssessmentDate = mark.Assessment?.Date ?? default,
        MarksObtained = mark.MarksObtained,
        MaximumMarks = mark.Assessment?.MaximumMarks ?? 0,
    };
}

// One row of an assessment's results — used by the teacher-facing results view.
public class AssessmentResultDto
{
    public int MarkId { get; set; }
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public int MarksObtained { get; set; }
    public int MaximumMarks { get; set; }
}

// Per-subject totals for a student — percentage and result category calculated on the backend.
public class MarksSummaryDto
{
    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public int Assessments { get; set; }
    public int TotalMarksObtained { get; set; }
    public int TotalMaximumMarks { get; set; }
    public double Percentage { get; set; }
    public string ResultCategory { get; set; } = string.Empty;
}

public class MarkCreateDto
{
    public int StudentId { get; set; }
    public int AssessmentId { get; set; }
    public int MarksObtained { get; set; }
}

public class MarkUpdateDto
{
    public int MarksObtained { get; set; }
}

// A teacher saving marks for a whole class at once.
public class BulkMarksRequestDto
{
    public int AssessmentId { get; set; }
    public List<BulkMarkItemDto> Marks { get; set; } = [];
}

public class BulkMarkItemDto
{
    public int StudentId { get; set; }
    public int MarksObtained { get; set; }
}
