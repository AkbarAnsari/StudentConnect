using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;

namespace StudentConnect.Infrastructure.Data.Stores;

public class NotificationStore(Database db)
{
    private static Notification Map(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32("Id"),
        UserId = reader.GetInt32("UserId"),
        Title = reader.GetString("Title"),
        Message = reader.GetString("Message"),
        Type = reader.GetEnum<NotificationType>("Type"),
        IsRead = reader.GetBoolean("IsRead"),
        CreatedAtUtc = reader.GetDateTimeUtc("CreatedAtUtc"),
    };

    public async Task<List<Notification>> GetForUserAsync(int userId) =>
        await db.QueryAsync(
            """
            SELECT "Id", "UserId", "Title", "Message", "Type", "IsRead", "CreatedAtUtc"
            FROM "Notifications"
            WHERE "UserId" = @userId
            ORDER BY "CreatedAtUtc" DESC
            LIMIT 50
            """,
            p => p.AddWithValue("userId", userId),
            Map);

    public async Task<int> GetUnreadCountAsync(int userId)
    {
        var count = await db.ExecuteScalarAsync<long>(
            """SELECT COUNT(*) FROM "Notifications" WHERE "UserId" = @userId AND "IsRead" = false""",
            p => p.AddWithValue("userId", userId));
        return (int)count;
    }

    public async Task InsertManyAsync(IEnumerable<int> userIds, string title, string message, NotificationType type)
    {
        var now = DateTime.UtcNow;
        foreach (var userId in userIds.Distinct())
        {
            await db.ExecuteAsync(
                """
                INSERT INTO "Notifications" ("UserId", "Title", "Message", "Type", "IsRead", "CreatedAtUtc")
                VALUES (@userId, @title, @message, @type, false, @createdAt)
                """,
                p =>
                {
                    p.AddWithValue("userId", userId);
                    p.AddWithValue("title", title);
                    p.AddWithValue("message", message);
                    p.AddWithValue("type", type.ToString());
                    p.AddWithValue("createdAt", now);
                });
        }
    }

    // Ownership-checked: only marks the row read if it belongs to the given user.
    public async Task<int> MarkAsReadAsync(int id, int userId) =>
        await db.ExecuteAsync(
            """UPDATE "Notifications" SET "IsRead" = true WHERE "Id" = @id AND "UserId" = @userId""",
            p =>
            {
                p.AddWithValue("id", id);
                p.AddWithValue("userId", userId);
            });

    public async Task MarkAllAsReadAsync(int userId) =>
        await db.ExecuteAsync(
            """UPDATE "Notifications" SET "IsRead" = true WHERE "UserId" = @userId AND "IsRead" = false""",
            p => p.AddWithValue("userId", userId));

    // Distinct user ids with digest enabled who have at least one not-yet-digested notification
    // — polled by NotificationDigestProcessor instead of being woken up by an enqueue.
    public async Task<List<int>> GetUserIdsWithPendingDigestAsync() =>
        await db.QueryAsync(
            """
            SELECT DISTINCT n."UserId"
            FROM "Notifications" n
            JOIN "Users" u ON u."Id" = n."UserId"
            WHERE n."DigestedAtUtc" IS NULL AND u."NotificationDigestEnabled" = true
            """,
            bind: null,
            reader => reader.GetInt32("UserId"));

    public async Task<List<Notification>> GetUndigestedForUserAsync(int userId) =>
        await db.QueryAsync(
            """
            SELECT "Id", "UserId", "Title", "Message", "Type", "IsRead", "CreatedAtUtc"
            FROM "Notifications"
            WHERE "UserId" = @userId AND "DigestedAtUtc" IS NULL
            ORDER BY "CreatedAtUtc"
            """,
            p => p.AddWithValue("userId", userId),
            Map);

    public async Task MarkDigestedAsync(IReadOnlyCollection<int> ids)
    {
        if (ids.Count == 0)
        {
            return;
        }

        await db.ExecuteAsync(
            """UPDATE "Notifications" SET "DigestedAtUtc" = now() WHERE "Id" = ANY(@ids)""",
            p => p.AddWithValue("ids", ids.ToArray()));
    }
}
