using StudentConnect.Application;
using StudentConnect.Domain;
using StudentConnect.Infrastructure.Data.Stores;

namespace StudentConnect.Infrastructure.Auditing;

public class AuditService(AuditLogStore auditLogs, ICurrentUserService currentUser) : IAuditService
{
    public async Task LogAsync(string action, string entityType, string entityId, string? details = null) =>
        await auditLogs.InsertAsync(new AuditLog
        {
            UserId = currentUser.UserId,
            Username = currentUser.Username ?? "unknown",
            Role = currentUser.Role ?? "unknown",
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
        });
}
