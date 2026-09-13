using StudentConnect.Api.Auditing;

namespace StudentConnect.Api.Tests;

// No-op stand-in for tests that construct a controller directly and don't care about audit
// side effects — mirrors NullLogger<T>.Instance's role for ILogger<T>.
public class NullAuditService : IAuditService
{
    public static readonly NullAuditService Instance = new();

    public Task LogAsync(string action, string entityType, string entityId, string? details = null) => Task.CompletedTask;
}
