namespace StudentConnect.Domain;

// Mirrors the Angular Student concept, now with GradeId and SectionId foreign keys
// instead of plain Grade/Section strings — the "many" side of both relationships.
public class Student
{
    public int Id { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int SchoolId { get; set; }

    public int GradeId { get; set; }
    public Grade? Grade { get; set; }

    public int? SectionId { get; set; }
    public Section? Section { get; set; }

    public StudentStatus Status { get; set; } = StudentStatus.Active;
    public DateTime? StatusChangedAtUtc { get; set; }
    public string? StatusReason { get; set; }

    public DateOnly? DateOfBirth { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
}
