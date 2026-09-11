namespace StudentConnect.Api.Models;

public class HomeworkDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public int GradeId { get; set; }
    public string GradeName { get; set; } = string.Empty;
    public int TeacherId { get; set; }
    public string TeacherName { get; set; } = string.Empty;
    public DateOnly CreatedDate { get; set; }
    public DateOnly DueDate { get; set; }

    public static HomeworkDto FromEntity(Homework homework) => new()
    {
        Id = homework.Id,
        Title = homework.Title,
        Description = homework.Description,
        SubjectId = homework.SubjectId,
        SubjectName = homework.Subject?.Name ?? string.Empty,
        GradeId = homework.GradeId,
        GradeName = homework.Grade?.Name ?? string.Empty,
        TeacherId = homework.TeacherId,
        TeacherName = homework.Teacher is null ? string.Empty : $"{homework.Teacher.FirstName} {homework.Teacher.LastName}",
        CreatedDate = homework.CreatedDate,
        DueDate = homework.DueDate,
    };
}

// What a student's homework list shows — status is derived, never stored redundantly.
public class StudentHomeworkDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public DateOnly DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class HomeworkCreateDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SubjectId { get; set; }
    public int GradeId { get; set; }
    public int TeacherId { get; set; }
    public DateOnly DueDate { get; set; }
}

// Only the fields a teacher can realistically correct after creation — not the subject/grade/teacher relationships.
public class HomeworkUpdateDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateOnly DueDate { get; set; }
}
