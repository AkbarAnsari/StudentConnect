using StudentConnect.Domain;

namespace StudentConnect.Application;

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
    public string? Comment { get; set; }
    public int Version { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

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
        Comment = mark.Comment,
        Version = mark.Version,
        UpdatedAtUtc = mark.UpdatedAtUtc,
    };
}

// One row of an assessment's results — used by the teacher-facing results view.
public class AssessmentResultDto
{
    public int MarkId { get; set; }
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public int MarksObtained { get; set; }
    public int MaximumMarks { get; set; }
    public string? Comment { get; set; }
    public int Version { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
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

    // Intermediate values used only to compute the weighted Percentage above; not
    // particularly meaningful to the frontend on their own.
    public decimal WeightedScore { get; set; }
    public decimal TotalWeight { get; set; }
}

public class MarkCreateDto
{
    public int StudentId { get; set; }
    public int AssessmentId { get; set; }
    public int MarksObtained { get; set; }
    public string? Comment { get; set; }
}

public class MarkUpdateDto
{
    public int MarksObtained { get; set; }
    public string? Comment { get; set; }
    public int ExpectedVersion { get; set; }
}

// A teacher saving marks for a whole class at once.
public class BulkMarksRequestDto
{
    public int SectionId { get; set; }
    public int AssessmentId { get; set; }
    public List<BulkMarkItemDto> Marks { get; set; } = [];
}

public class BulkMarkItemDto
{
    public int StudentId { get; set; }
    public int MarksObtained { get; set; }
    public string? Comment { get; set; }
    public int? ExpectedVersion { get; set; }
}

public class MarkHistoryDto
{
    public long Id { get; set; }
    public int MarkId { get; set; }
    public int PreviousMarks { get; set; }
    public int NewMarks { get; set; }
    public string? PreviousComment { get; set; }
    public string? NewComment { get; set; }
    public int Version { get; set; }
    public int ChangedByUserId { get; set; }
    public string ChangedByName { get; set; } = string.Empty;
    public DateTime ChangedAtUtc { get; set; }
}
