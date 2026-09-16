using StudentConnect.Domain;

namespace StudentConnect.Application;

public class SectionDto
{
    public int Id { get; set; }
    public int GradeId { get; set; }
    public string GradeName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? HomeroomTeacherId { get; set; }
    public string? HomeroomTeacherName { get; set; }
    public bool IsArchived { get; set; }

    public static SectionDto FromEntity(Section section) => new()
    {
        Id = section.Id,
        GradeId = section.GradeId,
        GradeName = section.Grade?.Name ?? string.Empty,
        Name = section.Name,
        HomeroomTeacherId = section.HomeroomTeacherId,
        HomeroomTeacherName = section.HomeroomTeacher is { } teacher ? $"{teacher.FirstName} {teacher.LastName}" : null,
        IsArchived = section.IsArchived,
    };
}

public class SectionCreateDto
{
    public int GradeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? HomeroomTeacherId { get; set; }
}

public class SectionUpdateDto
{
    public string Name { get; set; } = string.Empty;
    public int? HomeroomTeacherId { get; set; }
}

// A single row of a teacher's "My Classes" roster — deliberately minimal, not the full
// StudentResponseDto, since the section already tells you the grade/section context.
public class SectionStudentDto
{
    public int Id { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public static SectionStudentDto FromEntity(Student student) => new()
    {
        Id = student.Id,
        StudentId = student.StudentId,
        FirstName = student.FirstName,
        LastName = student.LastName,
        Email = student.Email,
    };
}


