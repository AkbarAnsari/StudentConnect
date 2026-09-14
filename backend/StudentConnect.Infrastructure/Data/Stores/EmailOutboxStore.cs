using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;

namespace StudentConnect.Infrastructure.Data.Stores;

public class EmailOutboxStore(Database db)
{
    private static EmailOutboxEntry Map(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32("Id"),
        ToEmail = reader.GetString("ToEmail"),
        ToUserId = reader.GetNullableInt32("ToUserId"),
        Subject = reader.GetString("Subject"),
        Body = reader.GetString("Body"),
        CreatedAtUtc = reader.GetDateTimeUtc("CreatedAtUtc"),
        SentAtUtc = reader.GetNullableDateTimeUtc("SentAtUtc"),
        Attempts = reader.GetInt32("Attempts"),
        LastError = reader.GetNullableString("LastError"),
    };

    public async Task EnqueueAsync(string toEmail, int? toUserId, string subject, string body) =>
        await db.ExecuteAsync(
            """
            INSERT INTO "EmailOutbox" ("ToEmail", "ToUserId", "Subject", "Body")
            VALUES (@toEmail, @toUserId, @subject, @body)
            """,
            p =>
            {
                p.AddWithValue("toEmail", toEmail);
                p.AddWithValue("toUserId", (object?)toUserId ?? DBNull.Value);
                p.AddWithValue("subject", subject);
                p.AddWithValue("body", body);
            });

    // Skips rows that have already failed too many times — left in the table (LastError
    // stays visible in the admin view) but not retried forever.
    public async Task<List<EmailOutboxEntry>> GetPendingAsync(int take, int maxAttempts) =>
        await db.QueryAsync(
            """
            SELECT "Id", "ToEmail", "ToUserId", "Subject", "Body", "CreatedAtUtc", "SentAtUtc", "Attempts", "LastError"
            FROM "EmailOutbox"
            WHERE "SentAtUtc" IS NULL AND "Attempts" < @maxAttempts
            ORDER BY "CreatedAtUtc"
            LIMIT @take
            """,
            p =>
            {
                p.AddWithValue("take", take);
                p.AddWithValue("maxAttempts", maxAttempts);
            },
            Map);

    public async Task MarkSentAsync(int id) =>
        await db.ExecuteAsync(
            """UPDATE "EmailOutbox" SET "SentAtUtc" = now(), "Attempts" = "Attempts" + 1 WHERE "Id" = @id""",
            p => p.AddWithValue("id", id));

    public async Task MarkFailedAsync(int id, string error) =>
        await db.ExecuteAsync(
            """UPDATE "EmailOutbox" SET "Attempts" = "Attempts" + 1, "LastError" = @error WHERE "Id" = @id""",
            p =>
            {
                p.AddWithValue("id", id);
                p.AddWithValue("error", error);
            });

    // Manual admin retry: resets the attempt counter and clears the error so the background
    // processor's normal GetPendingAsync poll picks the row up again.
    public async Task RetryAsync(int id) =>
        await db.ExecuteAsync(
            """UPDATE "EmailOutbox" SET "Attempts" = 0, "LastError" = NULL WHERE "Id" = @id AND "SentAtUtc" IS NULL""",
            p => p.AddWithValue("id", id));

    // Tenant-scoped via the recipient's SchoolId (every real EnqueueAsync call passes a
    // toUserId — see EmailNotificationService/AuthController/SchoolsController).
    public async Task<bool> ExistsAsync(int id, int schoolId) =>
        await db.ExistsAsync(
            """
            SELECT 1 FROM "EmailOutbox" eo JOIN "Users" u ON u."Id" = eo."ToUserId"
            WHERE eo."Id" = @id AND u."SchoolId" = @schoolId
            """,
            p =>
            {
                p.AddWithValue("id", id);
                p.AddWithValue("schoolId", schoolId);
            });

    // Newest first — used by the admin Email Outbox page. Tenant-scoped via the recipient's
    // SchoolId, same rationale as ExistsAsync above.
    public async Task<List<EmailOutboxEntry>> GetRecentAsync(int schoolId, int take) =>
        await db.QueryAsync(
            """
            SELECT eo."Id", eo."ToEmail", eo."ToUserId", eo."Subject", eo."Body", eo."CreatedAtUtc", eo."SentAtUtc", eo."Attempts", eo."LastError"
            FROM "EmailOutbox" eo
            JOIN "Users" u ON u."Id" = eo."ToUserId"
            WHERE u."SchoolId" = @schoolId
            ORDER BY eo."CreatedAtUtc" DESC
            LIMIT @take
            """,
            p =>
            {
                p.AddWithValue("schoolId", schoolId);
                p.AddWithValue("take", take);
            },
            Map);

    // Used by the admin Operations page's dependency status summary. Tenant-scoped like
    // GetRecentAsync above.
    public async Task<(long Pending, long Failed)> GetStatsAsync(int schoolId)
    {
        const string sql = """
            SELECT
                COUNT(*) FILTER (WHERE eo."SentAtUtc" IS NULL AND eo."LastError" IS NULL) AS "Pending",
                COUNT(*) FILTER (WHERE eo."SentAtUtc" IS NULL AND eo."LastError" IS NOT NULL) AS "Failed"
            FROM "EmailOutbox" eo
            JOIN "Users" u ON u."Id" = eo."ToUserId"
            WHERE u."SchoolId" = @schoolId
            """;

        var row = await db.QuerySingleOrDefaultAsync(
            sql,
            bind: p => p.AddWithValue("schoolId", schoolId),
            map: reader => new { Pending = reader.GetInt64("Pending"), Failed = reader.GetInt64("Failed") });

        return row is null ? (0, 0) : (row.Pending, row.Failed);
    }
}
