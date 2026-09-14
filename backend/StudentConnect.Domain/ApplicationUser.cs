namespace StudentConnect.Domain;

// Identity/User is a DIFFERENT concept from Student/Teacher — this is the account that can
// log in; Student/Teacher are the business/domain profiles it's optionally linked to.
// Development-only: a real application would use a proper identity provider (see docs/phase-10.md).
public class ApplicationUser
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public int SchoolId { get; set; }

    // Set for Student-role users only.
    public int? StudentId { get; set; }
    public Student? Student { get; set; }

    // Set for Teacher-role users only. Admins have both StudentId and TeacherId null.
    public int? TeacherId { get; set; }
    public Teacher? Teacher { get; set; }

    // Only set directly for Parent-role users (Student/Teacher accounts resolve their
    // email through the linked Student/Teacher row instead — see UserStore.GetEmailForUserAsync).
    public string? Email { get; set; }

    // Null until the address on file has been confirmed via the emailed verification link.
    public DateTime? EmailVerifiedAtUtc { get; set; }

    // Opt-in: batch notification emails into one periodic digest instead of a realtime email
    // per event (see NotificationDigestProcessor) — primarily aimed at Parents.
    public bool NotificationDigestEnabled { get; set; }
}
