namespace StudentConnect.Api.Models;

// Replaces the old free-text Student.Section — a real, admin-manageable entity scoped
// to a Grade ("Grade 8 - Section A") instead of an arbitrary string per student.
public class Section
{
    public int Id { get; set; }

    public int GradeId { get; set; }
    public Grade? Grade { get; set; }

    public string Name { get; set; } = string.Empty;

    public int? HomeroomTeacherId { get; set; }
    public Teacher? HomeroomTeacher { get; set; }
}
