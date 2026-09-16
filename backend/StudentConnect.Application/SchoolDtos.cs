namespace StudentConnect.Application;

// Public, unauthenticated self-service onboarding: creates a brand-new School (tenant) and
// its first Admin user account in one call, logs that admin in, and enqueues email verification.
public class SchoolRegistrationRequestDto
{
    public string SchoolName { get; set; } = string.Empty;
    public string AdminUsername { get; set; } = string.Empty;
    public string AdminPassword { get; set; } = string.Empty;
    public string AdminEmail { get; set; } = string.Empty;
}
