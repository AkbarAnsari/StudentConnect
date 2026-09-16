using StudentConnect.Domain;

namespace StudentConnect.Application;

public class TeacherSubjectAssignmentDto
{
    public int Id { get; set; }
    public int TeacherId { get; set; }
    public string TeacherName { get; set; } = string.Empty;
    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public int SectionId { get; set; }
    public string SectionName { get; set; } = string.Empty;
    public int GradeId { get; set; }
    public string GradeName { get; set; } = string.Empty;

    public static TeacherSubjectAssignmentDto FromEntity(TeacherSubjectAssignment assignment) => new()
    {
        Id = assignment.Id,
        TeacherId = assignment.TeacherId,
        TeacherName = assignment.Teacher is { } teacher ? $"{teacher.FirstName} {teacher.LastName}" : string.Empty,
        SubjectId = assignment.SubjectId,
        SubjectName = assignment.Subject?.Name ?? string.Empty,
        SectionId = assignment.SectionId,
        SectionName = assignment.Section?.Name ?? string.Empty,
        GradeId = assignment.Section?.Grade?.Id ?? 0,
        GradeName = assignment.Section?.Grade?.Name ?? string.Empty,
    };
}

public class TeacherSubjectAssignmentCreateDto
{
    public int TeacherId { get; set; }
    public int SubjectId { get; set; }
    public int SectionId { get; set; }
}
