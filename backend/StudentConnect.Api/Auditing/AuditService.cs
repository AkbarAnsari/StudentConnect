using StudentConnect.Api.Auth;
using StudentConnect.Api.Data.Stores;
using StudentConnect.Api.Models;

namespace StudentConnect.Api.Auditing;

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
