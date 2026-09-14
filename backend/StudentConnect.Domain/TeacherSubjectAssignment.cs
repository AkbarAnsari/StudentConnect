namespace StudentConnect.Domain;

// A Teacher formally assigned to teach one Subject in one Section — an explicit,
// admin-managed record instead of an implicit "any teacher can touch any subject" assumption.
public class TeacherSubjectAssignment
{
    public int Id { get; set; }

    public int TeacherId { get; set; }
    public Teacher? Teacher { get; set; }

    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }

    public int SectionId { get; set; }
    public Section? Section { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
