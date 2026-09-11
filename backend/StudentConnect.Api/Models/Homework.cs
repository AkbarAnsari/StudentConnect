namespace StudentConnect.Api.Models;

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

    public int TeacherId { get; set; }
    public Teacher? Teacher { get; set; }

    public DateOnly CreatedDate { get; set; }
    public DateOnly DueDate { get; set; }
}
