namespace StudentConnect.Api.Models;

// Mirrors the Angular Student concept, now with a GradeId foreign key
// instead of a plain Grade number — the "many" side of the relationship.
public class Student
{
    public int Id { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;

    public int GradeId { get; set; }
    public Grade? Grade { get; set; }
}
