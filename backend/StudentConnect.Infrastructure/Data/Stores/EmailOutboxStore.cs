using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;

namespace StudentConnect.Infrastructure.Data.Stores;

public class EmailOutboxStore(Database db)
{
    public sealed record PagedEntries(List<EmailOutboxEntry> Items, int TotalCount);
    public sealed record DeliveryStats(long Pending, long Failed, long SentLast24Hours, DateTime? OldestPendingAtUtc, DateTime? LastSentAtUtc, DateTime? LastFailureAtUtc);
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
        DeadLetterOwnerUserId = reader.GetNullableInt32("DeadLetterOwnerUserId"),
        DeadLetterAssignedAtUtc = reader.GetNullableDateTimeUtc("DeadLetterAssignedAtUtc"),
        ResolutionNotes = reader.GetNullableString("ResolutionNotes"),
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
            SELECT "Id", "ToEmail", "ToUserId", "Subject", "Body", "CreatedAtUtc", "SentAtUtc", "Attempts", "LastError", "DeadLetterOwnerUserId", "DeadLetterAssignedAtUtc", "ResolutionNotes"
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
        public async Task<int> RetryFailedAsync(int id, int schoolId) =>
                await db.ExecuteAsync(
                        """
                        UPDATE "EmailOutbox" eo SET "Attempts" = 0, "LastError" = NULL
                        WHERE eo."Id" = @id AND eo."SentAtUtc" IS NULL AND eo."LastError" IS NOT NULL
                            AND EXISTS (SELECT 1 FROM "Users" u WHERE u."Id" = eo."ToUserId" AND u."SchoolId" = @schoolId)
                        """,
                        p => { p.AddWithValue("id", id); p.AddWithValue("schoolId", schoolId); });

        public async Task<int> RetryFailedAsync(IReadOnlyCollection<int> ids, int schoolId) =>
                ids.Count == 0 ? 0 : await db.ExecuteAsync(
                        """
                        UPDATE "EmailOutbox" eo SET "Attempts" = 0, "LastError" = NULL
                        WHERE eo."Id" = ANY(@ids) AND eo."SentAtUtc" IS NULL AND eo."LastError" IS NOT NULL
                            AND EXISTS (SELECT 1 FROM "Users" u WHERE u."Id" = eo."ToUserId" AND u."SchoolId" = @schoolId)
                        """,
                        p => { p.AddWithValue("ids", ids.Distinct().ToArray()); p.AddWithValue("schoolId", schoolId); });

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
            SELECT eo."Id", eo."ToEmail", eo."ToUserId", eo."Subject", eo."Body", eo."CreatedAtUtc", eo."SentAtUtc", eo."Attempts", eo."LastError", eo."DeadLetterOwnerUserId", eo."DeadLetterAssignedAtUtc", eo."ResolutionNotes"
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

    public async Task<PagedEntries> GetPagedAsync(int schoolId, int page, int pageSize, string? status, string? query, DateTime? fromUtc, DateTime? toUtc)
    {
        const string filters = """
            WHERE u."SchoolId" = @schoolId
              AND (@status IS NULL
                OR (@status = 'sent' AND eo."SentAtUtc" IS NOT NULL)
                OR (@status = 'failed' AND eo."SentAtUtc" IS NULL AND eo."LastError" IS NOT NULL)
                OR (@status = 'pending' AND eo."SentAtUtc" IS NULL AND eo."LastError" IS NULL))
              AND (@query IS NULL OR eo."ToEmail" ILIKE @query ESCAPE '\' OR eo."Subject" ILIKE @query ESCAPE '\')
              AND (@fromUtc IS NULL OR eo."CreatedAtUtc" >= @fromUtc)
              AND (@toUtc IS NULL OR eo."CreatedAtUtc" < @toUtc)
            """;
        var total = await db.ExecuteScalarAsync<long>(
            $"""SELECT COUNT(*) FROM "EmailOutbox" eo JOIN "Users" u ON u."Id" = eo."ToUserId" {filters}""",
            p => BindFilters(p, schoolId, status, query, fromUtc, toUtc));
        var items = await db.QueryAsync(
            $"""
            SELECT eo."Id", eo."ToEmail", eo."ToUserId", eo."Subject", eo."Body", eo."CreatedAtUtc", eo."SentAtUtc", eo."Attempts", eo."LastError", eo."DeadLetterOwnerUserId", eo."DeadLetterAssignedAtUtc", eo."ResolutionNotes"
            FROM "EmailOutbox" eo JOIN "Users" u ON u."Id" = eo."ToUserId"
            {filters}
            ORDER BY eo."CreatedAtUtc" DESC, eo."Id" DESC
            OFFSET @offset LIMIT @pageSize
            """,
            p =>
            {
                BindFilters(p, schoolId, status, query, fromUtc, toUtc);
                p.AddWithValue("offset", (page - 1) * pageSize);
                p.AddWithValue("pageSize", pageSize);
            },
            Map);
        return new PagedEntries(items, (int)total);
    }

    public async Task<EmailOutboxEntry?> GetByIdAsync(int id, int schoolId) =>
        await db.QuerySingleOrDefaultAsync(
            """
            SELECT eo."Id", eo."ToEmail", eo."ToUserId", eo."Subject", eo."Body", eo."CreatedAtUtc", eo."SentAtUtc", eo."Attempts", eo."LastError", eo."DeadLetterOwnerUserId", eo."DeadLetterAssignedAtUtc", eo."ResolutionNotes"
            FROM "EmailOutbox" eo JOIN "Users" u ON u."Id" = eo."ToUserId"
            WHERE eo."Id" = @id AND u."SchoolId" = @schoolId
            """,
            p => { p.AddWithValue("id", id); p.AddWithValue("schoolId", schoolId); },
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

    public async Task<DeliveryStats> GetDeliveryStatsAsync(int schoolId)
    {
        var row = await db.QuerySingleOrDefaultAsync(
            """
            SELECT
                COUNT(*) FILTER (WHERE eo."SentAtUtc" IS NULL AND eo."LastError" IS NULL) AS "Pending",
                COUNT(*) FILTER (WHERE eo."SentAtUtc" IS NULL AND eo."LastError" IS NOT NULL) AS "Failed",
                COUNT(*) FILTER (WHERE eo."SentAtUtc" >= now() - interval '24 hours') AS "SentLast24Hours",
                MIN(eo."CreatedAtUtc") FILTER (WHERE eo."SentAtUtc" IS NULL AND eo."LastError" IS NULL) AS "OldestPendingAtUtc",
                MAX(eo."SentAtUtc") AS "LastSentAtUtc",
                MAX(eo."CreatedAtUtc") FILTER (WHERE eo."SentAtUtc" IS NULL AND eo."LastError" IS NOT NULL) AS "LastFailureAtUtc"
            FROM "EmailOutbox" eo JOIN "Users" u ON u."Id" = eo."ToUserId"
            WHERE u."SchoolId" = @schoolId
            """,
            p => p.AddWithValue("schoolId", schoolId),
            reader => new DeliveryStats(
                reader.GetInt64("Pending"), reader.GetInt64("Failed"), reader.GetInt64("SentLast24Hours"),
                reader.GetNullableDateTimeUtc("OldestPendingAtUtc"), reader.GetNullableDateTimeUtc("LastSentAtUtc"), reader.GetNullableDateTimeUtc("LastFailureAtUtc")));
        return row ?? new DeliveryStats(0, 0, 0, null, null, null);
    }

    public async Task<int> AssignDeadLetterAsync(int id,int schoolId,int ownerUserId,string? notes)=>await db.ExecuteAsync(
        """UPDATE "EmailOutbox" eo SET "DeadLetterOwnerUserId"=@ownerUserId,"DeadLetterAssignedAtUtc"=now(),"ResolutionNotes"=@notes WHERE eo."Id"=@id AND eo."SentAtUtc" IS NULL AND eo."LastError" IS NOT NULL AND EXISTS(SELECT 1 FROM "Users" u WHERE u."Id"=eo."ToUserId" AND u."SchoolId"=@schoolId)""",
        p=>{p.AddWithValue("id",id);p.AddWithValue("schoolId",schoolId);p.AddWithValue("ownerUserId",ownerUserId);p.AddWithValue("notes",(object?)notes??DBNull.Value);});

    private static void BindFilters(NpgsqlParameterCollection parameters, int schoolId, string? status, string? query, DateTime? fromUtc, DateTime? toUtc)
    {
        parameters.AddWithValue("schoolId", schoolId);
        parameters.Add("status", NpgsqlTypes.NpgsqlDbType.Text).Value = string.IsNullOrWhiteSpace(status) ? DBNull.Value : status.Trim().ToLowerInvariant();
        parameters.Add("query", NpgsqlTypes.NpgsqlDbType.Text).Value = string.IsNullOrWhiteSpace(query) ? DBNull.Value : $"%{EscapeLike(query.Trim())}%";
        parameters.Add("fromUtc", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = fromUtc is null ? DBNull.Value : fromUtc.Value;
        parameters.Add("toUtc", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = toUtc is null ? DBNull.Value : toUtc.Value;
    }

    private static string EscapeLike(string value) => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
