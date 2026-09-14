using StudentConnect.Domain;

namespace StudentConnect.Application;

// What Angular actually receives — flattens the Grade/Section relationships into
// gradeName/sectionName so the frontend never needs to know how the database models them.
public class StudentResponseDto
{
    public int Id { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int GradeId { get; set; }
    public string GradeName { get; set; } = string.Empty;
    public int? SectionId { get; set; }
    public string SectionName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? StatusChangedAtUtc { get; set; }
    public string? StatusReason { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }

    public static StudentResponseDto FromEntity(Student student) => new()
    {
        Id = student.Id,
        StudentId = student.StudentId,
        FirstName = student.FirstName,
        LastName = student.LastName,
        Email = student.Email,
        GradeId = student.GradeId,
        GradeName = student.Grade?.Name ?? string.Empty,
        SectionId = student.SectionId,
        SectionName = student.Section?.Name ?? string.Empty,
        Status = student.Status.ToString(),
        StatusChangedAtUtc = student.StatusChangedAtUtc,
        StatusReason = student.StatusReason,
        DateOfBirth = student.DateOfBirth,
        Phone = student.Phone,
        Address = student.Address,
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
    public int SectionId { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
}

// What Angular sends when editing a student.
public class StudentUpdateDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int GradeId { get; set; }
    public int SectionId { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
}

// What Angular sends to change a student's lifecycle status.
public class StudentStatusUpdateDto
{
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
}
