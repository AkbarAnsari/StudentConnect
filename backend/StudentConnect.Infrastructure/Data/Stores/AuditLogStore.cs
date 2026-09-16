using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;

namespace StudentConnect.Infrastructure.Data.Stores;

public class AuditLogStore(Database db)
{
    public sealed record PagedLogs(List<AuditLog> Items, int TotalCount);
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

    public async Task<PagedLogs> GetPagedAsync(
        int schoolId,
        int page,
        int pageSize,
        string? entityType,
        string? action,
        string? username,
        string? query,
        DateTime? fromUtc,
        DateTime? toUtc)
    {
        const string filters = """
            WHERE u."SchoolId" = @schoolId
              AND (@entityType IS NULL OR al."EntityType" = @entityType)
              AND (@action IS NULL OR al."Action" = @action)
              AND (@username IS NULL OR al."Username" ILIKE @username ESCAPE '\')
              AND (@query IS NULL OR al."EntityId" ILIKE @query ESCAPE '\' OR COALESCE(al."Details", '') ILIKE @query ESCAPE '\')
              AND (@fromUtc IS NULL OR al."TimestampUtc" >= @fromUtc)
              AND (@toUtc IS NULL OR al."TimestampUtc" < @toUtc)
            """;

        var total = await db.ExecuteScalarAsync<long>(
            $"""SELECT COUNT(*) FROM "AuditLogs" al JOIN "Users" u ON u."Id" = al."UserId" {filters}""",
            p => BindFilters(p, schoolId, entityType, action, username, query, fromUtc, toUtc));
        var items = await db.QueryAsync(
            $"""
            SELECT al."Id", al."TimestampUtc", al."UserId", al."Username", al."Role", al."Action", al."EntityType", al."EntityId", al."Details"
            FROM "AuditLogs" al JOIN "Users" u ON u."Id" = al."UserId"
            {filters}
            ORDER BY al."TimestampUtc" DESC, al."Id" DESC
            OFFSET @offset LIMIT @pageSize
            """,
            p =>
            {
                BindFilters(p, schoolId, entityType, action, username, query, fromUtc, toUtc);
                p.AddWithValue("offset", (page - 1) * pageSize);
                p.AddWithValue("pageSize", pageSize);
            },
            Map);
        return new PagedLogs(items, (int)total);
    }

    public async Task<AuditLog?> GetByIdAsync(long id, int schoolId) =>
        await db.QuerySingleOrDefaultAsync(
            """
            SELECT al."Id", al."TimestampUtc", al."UserId", al."Username", al."Role", al."Action", al."EntityType", al."EntityId", al."Details"
            FROM "AuditLogs" al JOIN "Users" u ON u."Id" = al."UserId"
            WHERE al."Id" = @id AND u."SchoolId" = @schoolId
            """,
            p => { p.AddWithValue("id", id); p.AddWithValue("schoolId", schoolId); },
            Map);

    public async Task<List<AuditLog>> GetEntityHistoryAsync(string entityType, string entityId, int schoolId, int take = 100) =>
        await db.QueryAsync(
            """
            SELECT al."Id", al."TimestampUtc", al."UserId", al."Username", al."Role", al."Action", al."EntityType", al."EntityId", al."Details"
            FROM "AuditLogs" al JOIN "Users" u ON u."Id" = al."UserId"
            WHERE al."EntityType" = @entityType AND al."EntityId" = @entityId AND u."SchoolId" = @schoolId
            ORDER BY al."TimestampUtc" DESC, al."Id" DESC LIMIT @take
            """,
            p => { p.AddWithValue("entityType", entityType); p.AddWithValue("entityId", entityId); p.AddWithValue("schoolId", schoolId); p.AddWithValue("take", take); },
            Map);

    private static void BindFilters(
        NpgsqlParameterCollection parameters,
        int schoolId,
        string? entityType,
        string? action,
        string? username,
        string? query,
        DateTime? fromUtc,
        DateTime? toUtc)
    {
        parameters.AddWithValue("schoolId", schoolId);
        parameters.Add("entityType", NpgsqlTypes.NpgsqlDbType.Text).Value = string.IsNullOrWhiteSpace(entityType) ? DBNull.Value : entityType.Trim();
        parameters.Add("action", NpgsqlTypes.NpgsqlDbType.Text).Value = string.IsNullOrWhiteSpace(action) ? DBNull.Value : action.Trim();
        parameters.Add("username", NpgsqlTypes.NpgsqlDbType.Text).Value = string.IsNullOrWhiteSpace(username) ? DBNull.Value : $"%{EscapeLike(username.Trim())}%";
        parameters.Add("query", NpgsqlTypes.NpgsqlDbType.Text).Value = string.IsNullOrWhiteSpace(query) ? DBNull.Value : $"%{EscapeLike(query.Trim())}%";
        parameters.Add("fromUtc", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = fromUtc is null ? DBNull.Value : fromUtc.Value;
        parameters.Add("toUtc", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = toUtc is null ? DBNull.Value : toUtc.Value;
    }

    private static string EscapeLike(string value) => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
