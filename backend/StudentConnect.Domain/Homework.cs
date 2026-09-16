namespace StudentConnect.Domain;

// The task itself — created by a teacher, belongs to a subject and a grade.
public class Homework
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }

    public int GradeId { get; set; }
    public Grade? Grade { get; set; }

    public int? SectionId { get; set; }
    public Section? Section { get; set; }

    public int TeacherId { get; set; }
    public Teacher? Teacher { get; set; }

    public DateOnly CreatedDate { get; set; }
    public DateOnly DueDate { get; set; }
    public ContentLifecycleStatus Status { get; set; } = ContentLifecycleStatus.Published;
    public DateTime? ScheduledForUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public DateTime? ArchivedAtUtc { get; set; }
}
