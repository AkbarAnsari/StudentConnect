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

    // How much this assessment counts toward a subject's overall percentage relative to other
    // assessments in the same subject (e.g. a FinalExam weighted 3x a Quiz). Defaults to 1 so
    // existing assessments behave exactly as before (simple equal-weight average).
    public decimal Weight { get; set; } = 1.0m;

    public AssessmentStatus Status { get; set; } = AssessmentStatus.Draft;
    public DateTime? PublishedAtUtc { get; set; }

    public int? TeacherSubjectAssignmentId { get; set; }
    public TeacherSubjectAssignment? TeacherSubjectAssignment { get; set; }
    // Optional — lets an assessment be tied to a Term for locking/cumulative grading.
    public int? TermId { get; set; }
    public Term? Term { get; set; }
}
