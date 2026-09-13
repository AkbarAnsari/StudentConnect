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
    public async Task<List<AuditLog>> GetRecentAsync(string? entityType, int take)
    {
        if (string.IsNullOrWhiteSpace(entityType))
        {
            return await db.QueryAsync(
                """SELECT "Id", "TimestampUtc", "UserId", "Username", "Role", "Action", "EntityType", "EntityId", "Details" FROM "AuditLogs" ORDER BY "TimestampUtc" DESC LIMIT @take""",
                p => p.AddWithValue("take", take),
                Map);
        }

        return await db.QueryAsync(
            """
            SELECT "Id", "TimestampUtc", "UserId", "Username", "Role", "Action", "EntityType", "EntityId", "Details"
            FROM "AuditLogs" WHERE "EntityType" = @entityType ORDER BY "TimestampUtc" DESC LIMIT @take
            """,
            p =>
            {
                p.AddWithValue("entityType", entityType);
                p.AddWithValue("take", take);
            },
            Map);
    }
}
