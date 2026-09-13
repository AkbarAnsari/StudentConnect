namespace StudentConnect.Api.Auditing;

// Records who did what to which entity — see AuditLogStore for the append-only table.
public interface IAuditService
{
    Task LogAsync(string action, string entityType, string entityId, string? details = null);
}
