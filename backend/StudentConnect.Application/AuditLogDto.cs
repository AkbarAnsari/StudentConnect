using StudentConnect.Domain;

namespace StudentConnect.Application;

public class AuditLogDto
{
    public long Id { get; set; }
    public DateTime TimestampUtc { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? Details { get; set; }

    public static AuditLogDto FromEntity(AuditLog log) => new()
    {
        Id = log.Id,
        TimestampUtc = log.TimestampUtc,
        Username = log.Username,
        Role = log.Role,
        Action = log.Action,
        EntityType = log.EntityType,
        EntityId = log.EntityId,
        Details = log.Details,
    };
}
