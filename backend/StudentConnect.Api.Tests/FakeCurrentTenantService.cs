using StudentConnect.Application;

namespace StudentConnect.Api.Tests;

// Fixed-tenant stand-in for tests that construct a controller directly — mirrors
// NullAuditService's role for IAuditService. Defaults to School Id 1 ("Demo School",
// seeded by database/seed.sql), matching the tenant every other seeded row belongs to.
public class FakeCurrentTenantService(int schoolId = 1) : ICurrentTenantService
{
    public int? SchoolId { get; } = schoolId;
}
