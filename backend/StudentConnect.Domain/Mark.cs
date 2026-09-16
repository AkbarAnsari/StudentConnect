namespace StudentConnect.Domain;

// One student's result for one assessment.
// Business rule: a student can only have one mark per assessment (enforced by a unique index).
public class Mark
{
    public int Id { get; set; }

    public int StudentId { get; set; }
    public Student? Student { get; set; }

    public int AssessmentId { get; set; }
    public Assessment? Assessment { get; set; }

    public int MarksObtained { get; set; }
    public string? Comment { get; set; }
    public int Version { get; set; } = 1;
    public DateTime? UpdatedAtUtc { get; set; }
    public int? UpdatedByUserId { get; set; }
}
