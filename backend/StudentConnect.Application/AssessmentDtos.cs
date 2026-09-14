using StudentConnect.Domain;

namespace StudentConnect.Application;

// What Angular receives — the assessment plus its subject name, no raw foreign keys alone.
public class AssessmentDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public int MaximumMarks { get; set; }
    public string AssessmentType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int? TermId { get; set; }
    public string? TermName { get; set; }

    public static AssessmentDto FromEntity(Assessment assessment) => new()
    {
        Id = assessment.Id,
        Title = assessment.Title,
        SubjectId = assessment.SubjectId,
        SubjectName = assessment.Subject?.Name ?? string.Empty,
        Date = assessment.Date,
        MaximumMarks = assessment.MaximumMarks,
        AssessmentType = assessment.AssessmentType.ToString(),
        Description = assessment.Description,
        TermId = assessment.TermId,
        TermName = assessment.Term?.Name,
    };
}

public class AssessmentCreateDto
{
    public string Title { get; set; } = string.Empty;
    public int SubjectId { get; set; }
    public DateOnly Date { get; set; }
    public int MaximumMarks { get; set; }
    public string AssessmentType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int? TermId { get; set; }
}

public class AssessmentUpdateDto
{
    public string Title { get; set; } = string.Empty;
    public int SubjectId { get; set; }
    public DateOnly Date { get; set; }
    public int MaximumMarks { get; set; }
    public string AssessmentType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int? TermId { get; set; }
}
