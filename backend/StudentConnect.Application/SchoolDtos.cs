namespace StudentConnect.Application;

// Public, unauthenticated self-service onboarding: creates a brand-new School (tenant) and
// its first Admin user account in one call, and logs that admin in immediately. No email
// verification in this pass — see docs/solution-architecture-multi-tenancy.md for what's
// deliberately deferred.
public class SchoolRegistrationRequestDto
{
    public string SchoolName { get; set; } = string.Empty;
    public string AdminUsername { get; set; } = string.Empty;
    public string AdminPassword { get; set; } = string.Empty;
    public string AdminEmail { get; set; } = string.Empty;
}
