namespace StudentConnect.Api.Models;

// What Angular actually receives — flattens the Grade relationship into gradeName
// so the frontend never needs to know how the database models the relationship.
public class StudentResponseDto
{
    public int Id { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public int GradeId { get; set; }
    public string GradeName { get; set; } = string.Empty;

    public static StudentResponseDto FromEntity(Student student) => new()
    {
        Id = student.Id,
        StudentId = student.StudentId,
        FirstName = student.FirstName,
        LastName = student.LastName,
        Email = student.Email,
        Section = student.Section,
        GradeId = student.GradeId,
        GradeName = student.Grade?.Name ?? string.Empty,
    };
}

// What Angular sends when creating a student.
public class StudentCreateDto
{
    public string StudentId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int GradeId { get; set; }
    public string Section { get; set; } = string.Empty;
}

// What Angular sends when editing a student.
public class StudentUpdateDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int GradeId { get; set; }
    public string Section { get; set; } = string.Empty;
}
