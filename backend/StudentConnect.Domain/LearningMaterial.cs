namespace StudentConnect.Domain;

// Metadata ABOUT a file — the actual bytes live in file storage, not in SQL Server.
public class LearningMaterial
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;

    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }

    public int GradeId { get; set; }
    public Grade? Grade { get; set; }

    public int? SectionId { get; set; }
    public Section? Section { get; set; }

    public int TeacherId { get; set; }
    public Teacher? Teacher { get; set; }

    // The name the teacher uploaded it with — used when the student downloads it.
    public string FileName { get; set; } = string.Empty;

    // A generated, collision-proof name used only inside file storage — never shown to Angular.
    public string StoredFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime UploadedDate { get; set; }
    public int Version { get; set; } = 1;
    public ContentLifecycleStatus Status { get; set; } = ContentLifecycleStatus.Published;
    public DateTime? ScheduledForUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public DateTime? ArchivedAtUtc { get; set; }
}
