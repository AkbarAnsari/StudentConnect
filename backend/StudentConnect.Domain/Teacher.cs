namespace StudentConnect.Domain;

// Also the login identity for the Teacher role (see Users.TeacherId) — a Teacher row can
// exist without a login (e.g. seeded for record-keeping only) or with one.
public class Teacher
{
    public int Id { get; set; }
    public string TeacherId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
