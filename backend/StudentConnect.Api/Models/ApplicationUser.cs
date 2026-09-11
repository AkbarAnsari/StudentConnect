namespace StudentConnect.Api.Models;

// Identity/User is a DIFFERENT concept from Student/Teacher — this is the account that can
// log in; Student/Teacher are the business/domain profiles it's optionally linked to.
// Development-only: a real application would use a proper identity provider (see docs/phase-10.md).
public class ApplicationUser
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }

    // Set for Student-role users only.
    public int? StudentId { get; set; }
    public Student? Student { get; set; }

    // Set for Teacher-role users only. Admins have both StudentId and TeacherId null.
    public int? TeacherId { get; set; }
    public Teacher? Teacher { get; set; }
}
