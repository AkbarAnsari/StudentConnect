namespace StudentConnect.Domain;

// Describes the test/exam itself — not any particular student's performance in it.
public class Assessment
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;

    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }

    public DateOnly Date { get; set; }
    public int MaximumMarks { get; set; }
    public AssessmentType AssessmentType { get; set; }
    public string Description { get; set; } = string.Empty;

    // Optional — lets an assessment be tied to a Term for locking/cumulative grading.
    public int? TermId { get; set; }
    public Term? Term { get; set; }
}
