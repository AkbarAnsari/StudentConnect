namespace StudentConnect.Domain;

// Links a Parent-role ApplicationUser to the Student(s) they may view — a parent can
// have multiple children, and (in principle) a student could have multiple linked
// parents/guardians, so this is a many-to-many join, not a single FK on either side.
public class ParentStudent
{
    public int Id { get; set; }

    public int ParentUserId { get; set; }
    public ApplicationUser? ParentUser { get; set; }

    public int StudentId { get; set; }
    public Student? Student { get; set; }
}
