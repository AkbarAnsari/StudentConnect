using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;

namespace StudentConnect.Infrastructure.Data.Stores;

public class AuditLogStore(Database db)
{
    private static AuditLog Map(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt64("Id"),
        TimestampUtc = reader.GetDateTimeUtc("TimestampUtc"),
        UserId = reader.GetNullableInt32("UserId"),
        Username = reader.GetString("Username"),
        Role = reader.GetString("Role"),
        Action = reader.GetString("Action"),
        EntityType = reader.GetString("EntityType"),
        EntityId = reader.GetString("EntityId"),
        Details = reader.GetNullableString("Details"),
    };

    public async Task InsertAsync(AuditLog entry) =>
        await db.ExecuteAsync(
            """
            INSERT INTO "AuditLogs" ("UserId", "Username", "Role", "Action", "EntityType", "EntityId", "Details")
            VALUES (@userId, @username, @role, @action, @entityType, @entityId, @details)
            """,
            p =>
            {
                p.AddWithValue("userId", (object?)entry.UserId ?? DBNull.Value);
                p.AddWithValue("username", entry.Username);
                p.AddWithValue("role", entry.Role);
                p.AddWithValue("action", entry.Action);
                p.AddWithValue("entityType", entry.EntityType);
                p.AddWithValue("entityId", entry.EntityId);
                p.AddWithValue("details", (object?)entry.Details ?? DBNull.Value);
            });

    // Newest first, optionally narrowed to one entity type — used by the admin Audit Log page.
    // Tenant-scoped via the acting user's SchoolId (every real audit entry has a UserId — see
    // AuditService — so an inner join never hides a legitimate row).
    public async Task<List<AuditLog>> GetRecentAsync(int schoolId, string? entityType, int take)
    {
        if (string.IsNullOrWhiteSpace(entityType))
        {
            return await db.QueryAsync(
                """
                SELECT al."Id", al."TimestampUtc", al."UserId", al."Username", al."Role", al."Action", al."EntityType", al."EntityId", al."Details"
                FROM "AuditLogs" al
                JOIN "Users" u ON u."Id" = al."UserId"
                WHERE u."SchoolId" = @schoolId
                ORDER BY al."TimestampUtc" DESC LIMIT @take
                """,
                p =>
                {
                    p.AddWithValue("schoolId", schoolId);
                    p.AddWithValue("take", take);
                },
                Map);
        }

        return await db.QueryAsync(
            """
            SELECT al."Id", al."TimestampUtc", al."UserId", al."Username", al."Role", al."Action", al."EntityType", al."EntityId", al."Details"
            FROM "AuditLogs" al
            JOIN "Users" u ON u."Id" = al."UserId"
            WHERE u."SchoolId" = @schoolId AND al."EntityType" = @entityType
            ORDER BY al."TimestampUtc" DESC LIMIT @take
            """,
            p =>
            {
                p.AddWithValue("schoolId", schoolId);
                p.AddWithValue("entityType", entityType);
                p.AddWithValue("take", take);
            },
            Map);
    }
}
