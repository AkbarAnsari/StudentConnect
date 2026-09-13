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

    public int GradeId { get; set; }
    public Grade? Grade { get; set; }

    public int? SectionId { get; set; }
    public Section? Section { get; set; }
}
