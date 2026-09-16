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
        Priority = reader.GetString("Priority"),
        ActionUrl = reader.GetNullableString("ActionUrl"),
        AnnouncementId = reader.GetNullableInt32("AnnouncementId"),
        HomeworkId = reader.GetNullableInt32("HomeworkId"),
        LearningMaterialId = reader.GetNullableInt32("LearningMaterialId"),
    };

    public async Task<List<Notification>> GetForUserAsync(int userId) =>
        await db.QueryAsync(
            """
            SELECT "Id", "UserId", "Title", "Message", "Type", "IsRead", "CreatedAtUtc", "Priority", "ActionUrl", "AnnouncementId", "HomeworkId", "LearningMaterialId"
            FROM "Notifications"
            WHERE "UserId" = @userId
            ORDER BY "CreatedAtUtc" DESC
            LIMIT 50
            """,
            p => p.AddWithValue("userId", userId),
            Map);

    public async Task<(List<Notification> Items, int TotalCount)> GetPagedForUserAsync(
        int userId,
        int page,
        int pageSize,
        bool unreadOnly,
        string? type)
    {
        var conditions = new List<string> { "\"UserId\" = @userId" };
        if (unreadOnly) conditions.Add("\"IsRead\" = false");
        if (!string.IsNullOrWhiteSpace(type)) conditions.Add("\"Type\" = @type");
        var where = "WHERE " + string.Join(" AND ", conditions);
        void Bind(NpgsqlParameterCollection p)
        {
            p.AddWithValue("userId", userId);
            if (!string.IsNullOrWhiteSpace(type)) p.AddWithValue("type", type);
        }

        var items = await db.QueryAsync(
            $"""
            SELECT "Id", "UserId", "Title", "Message", "Type", "IsRead", "CreatedAtUtc", "Priority", "ActionUrl", "AnnouncementId", "HomeworkId", "LearningMaterialId"
            FROM "Notifications" {where}
            ORDER BY "CreatedAtUtc" DESC
            LIMIT @pageSize OFFSET @offset
            """,
            p => { Bind(p); p.AddWithValue("pageSize", pageSize); p.AddWithValue("offset", (page - 1) * pageSize); },
            Map);
        var count = await db.ExecuteScalarAsync<long>($"""SELECT COUNT(*) FROM "Notifications" {where}""", Bind);
        return (items, (int)count);
    }

    public async Task<int> GetUnreadCountAsync(int userId)
    {
        var count = await db.ExecuteScalarAsync<long>(
            """SELECT COUNT(*) FROM "Notifications" WHERE "UserId" = @userId AND "IsRead" = false""",
            p => p.AddWithValue("userId", userId));
        return (int)count;
    }

    public async Task InsertManyAsync(
        IEnumerable<int> userIds,
        string title,
        string message,
        NotificationType type,
        string? actionUrl = null,
        string priority = "Normal",
        int? announcementId = null)
    {
        var now = DateTime.UtcNow;
        foreach (var userId in userIds.Distinct())
        {
            await db.ExecuteAsync(
                """
                INSERT INTO "Notifications" ("UserId", "Title", "Message", "Type", "IsRead", "CreatedAtUtc", "Priority", "ActionUrl", "AnnouncementId")
                VALUES (@userId, @title, @message, @type, false, @createdAt, @priority, @actionUrl, @announcementId)
                ON CONFLICT ("AnnouncementId", "UserId") WHERE "AnnouncementId" IS NOT NULL DO NOTHING
                """,
                p =>
                {
                    p.AddWithValue("userId", userId);
                    p.AddWithValue("title", title);
                    p.AddWithValue("message", message);
                    p.AddWithValue("type", type.ToString());
                    p.AddWithValue("createdAt", now);
                    p.AddWithValue("priority", priority);
                    p.AddWithValue("actionUrl", (object?)actionUrl ?? DBNull.Value);
                    p.AddWithValue("announcementId", (object?)announcementId ?? DBNull.Value);
                });
        }
    }

    public async Task<IReadOnlyList<int>> InsertManyForHomeworkAsync(
        IEnumerable<int> userIds,
        string title,
        string message,
        NotificationType type,
        string actionUrl,
        int homeworkId)
    {
        var insertedUserIds = new List<int>();
        foreach (var userId in userIds.Distinct())
        {
            var insertedId = await db.ExecuteScalarAsync<int>(
                """
                INSERT INTO "Notifications" ("UserId", "Title", "Message", "Type", "IsRead", "CreatedAtUtc", "Priority", "ActionUrl", "HomeworkId")
                VALUES (@userId, @title, @message, @type, false, now(), 'Normal', @actionUrl, @homeworkId)
                ON CONFLICT ("HomeworkId", "UserId") WHERE "HomeworkId" IS NOT NULL DO NOTHING
                RETURNING "Id"
                """,
                p =>
                {
                    p.AddWithValue("userId", userId);
                    p.AddWithValue("title", title);
                    p.AddWithValue("message", message);
                    p.AddWithValue("type", type.ToString());
                    p.AddWithValue("actionUrl", actionUrl);
                    p.AddWithValue("homeworkId", homeworkId);
                });
            if (insertedId > 0) insertedUserIds.Add(userId);
        }
        return insertedUserIds;
    }

    public async Task<IReadOnlyList<int>> InsertManyForLearningMaterialAsync(
        IEnumerable<int> userIds,
        string title,
        string message,
        NotificationType type,
        string actionUrl,
        int learningMaterialId)
    {
        var insertedUserIds = new List<int>();
        foreach (var userId in userIds.Distinct())
        {
            var insertedId = await db.ExecuteScalarAsync<int>(
                """
                INSERT INTO "Notifications" ("UserId", "Title", "Message", "Type", "IsRead", "CreatedAtUtc", "Priority", "ActionUrl", "LearningMaterialId")
                VALUES (@userId, @title, @message, @type, false, now(), 'Normal', @actionUrl, @learningMaterialId)
                ON CONFLICT ("LearningMaterialId", "UserId") WHERE "LearningMaterialId" IS NOT NULL DO NOTHING
                RETURNING "Id"
                """,
                p =>
                {
                    p.AddWithValue("userId", userId);
                    p.AddWithValue("title", title);
                    p.AddWithValue("message", message);
                    p.AddWithValue("type", type.ToString());
                    p.AddWithValue("actionUrl", actionUrl);
                    p.AddWithValue("learningMaterialId", learningMaterialId);
                });
            if (insertedId > 0) insertedUserIds.Add(userId);
        }
        return insertedUserIds;
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
            SELECT "Id", "UserId", "Title", "Message", "Type", "IsRead", "CreatedAtUtc", "Priority", "ActionUrl", "AnnouncementId", "HomeworkId", "LearningMaterialId"
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
