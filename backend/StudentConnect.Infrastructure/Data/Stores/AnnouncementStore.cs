using Npgsql;
using StudentConnect.Application;

namespace StudentConnect.Infrastructure.Data.Stores;

public sealed record AnnouncementDispatchItem(
    int Id,
    int SchoolId,
    int AuthorUserId,
    string Title,
    string Body,
    string ScopeType,
    int? ScopeGradeId,
    int? ScopeSectionId,
    string Priority,
    string Status,
    DateTime? ExpiresAtUtc);

public class AnnouncementStore(Database db)
{
    private const string SelectWithNames = """
        SELECT a."Id", a."Title", a."Body", a."ScopeType", a."CreatedAtUtc", a."Status", a."Priority",
               a."ScheduledForUtc", a."ExpiresAtUtc", a."SentAtUtc", a."WithdrawnAtUtc", a."RecipientCount",
               a."AttachmentFileName", a."AttachmentStoredFileName", a."AttachmentContentType", a."AttachmentFileSize", a."DispatchAttempts", a."LastDispatchError", a."UpdatedAtUtc",
               (SELECT COUNT(*) FROM "Notifications" n WHERE n."AnnouncementId" = a."Id" AND n."IsRead" = true) AS "ReadCount",
               g."Name" AS "GradeName", sec."Name" AS "SectionName",
               COALESCE(st."FirstName" || ' ' || st."LastName", t."FirstName" || ' ' || t."LastName", u."Username") AS "AuthorName"
        FROM "Announcements" a
        JOIN "Users" u ON u."Id" = a."AuthorUserId"
        LEFT JOIN "Students" st ON st."Id" = u."StudentId"
        LEFT JOIN "Teachers" t ON t."Id" = u."TeacherId"
        LEFT JOIN "Grades" g ON g."Id" = a."ScopeGradeId"
        LEFT JOIN "Sections" sec ON sec."Id" = a."ScopeSectionId"
        """;

    private static AnnouncementDto Map(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32("Id"),
        Title = reader.GetString("Title"),
        Body = reader.GetString("Body"),
        ScopeType = reader.GetString("ScopeType"),
        ScopeName = reader.GetNullableString("SectionName") ?? reader.GetNullableString("GradeName"),
        AuthorName = reader.GetNullableString("AuthorName") ?? "Unknown",
        CreatedAtUtc = reader.GetDateTimeUtc("CreatedAtUtc"),
        Status = reader.GetString("Status"),
        Priority = reader.GetString("Priority"),
        ScheduledForUtc = reader.GetNullableDateTimeUtc("ScheduledForUtc"),
        ExpiresAtUtc = reader.GetNullableDateTimeUtc("ExpiresAtUtc"),
        SentAtUtc = reader.GetNullableDateTimeUtc("SentAtUtc"),
        WithdrawnAtUtc = reader.GetNullableDateTimeUtc("WithdrawnAtUtc"),
        RecipientCount = reader.GetInt32("RecipientCount"),
        ReadCount = (int)reader.GetInt64("ReadCount"),
        AttachmentFileName = reader.GetNullableString("AttachmentFileName"),
        AttachmentFileSize = reader.GetNullableInt64("AttachmentFileSize"),
        DispatchAttempts = reader.GetInt32("DispatchAttempts"),
        LastDispatchError = reader.GetNullableString("LastDispatchError"),
        UpdatedAtUtc = reader.GetNullableDateTimeUtc("UpdatedAtUtc"),
    };

    public async Task<int> CreateAsync(
        int schoolId,
        int authorUserId,
        string title,
        string body,
        string scopeType,
        int? scopeGradeId,
        int? scopeSectionId,
        string status,
        string priority,
        DateTime? scheduledForUtc,
        DateTime? expiresAtUtc,
        int recipientCount,
        string? attachmentFileName = null,
        string? attachmentStoredFileName = null,
        string? attachmentContentType = null,
        long? attachmentFileSize = null) =>
        await db.ExecuteScalarAsync<int>(
            """
            INSERT INTO "Announcements" ("SchoolId", "AuthorUserId", "Title", "Body", "ScopeType", "ScopeGradeId", "ScopeSectionId", "Status", "Priority", "ScheduledForUtc", "ExpiresAtUtc", "SentAtUtc", "RecipientCount", "AttachmentFileName", "AttachmentStoredFileName", "AttachmentContentType", "AttachmentFileSize")
            VALUES (@schoolId, @authorUserId, @title, @body, @scopeType, @scopeGradeId, @scopeSectionId, @status, @priority, @scheduledForUtc, @expiresAtUtc, CASE WHEN @status = 'Sent' THEN now() ELSE NULL END, @recipientCount, @attachmentFileName, @attachmentStoredFileName, @attachmentContentType, @attachmentFileSize)
            RETURNING "Id"
            """,
            p =>
            {
                p.AddWithValue("schoolId", schoolId);
                p.AddWithValue("authorUserId", authorUserId);
                p.AddWithValue("title", title);
                p.AddWithValue("body", body);
                p.AddWithValue("scopeType", scopeType);
                p.AddWithValue("scopeGradeId", (object?)scopeGradeId ?? DBNull.Value);
                p.AddWithValue("scopeSectionId", (object?)scopeSectionId ?? DBNull.Value);
                p.AddWithValue("status", status);
                p.AddWithValue("priority", priority);
                p.AddWithValue("scheduledForUtc", (object?)scheduledForUtc ?? DBNull.Value);
                p.AddWithValue("expiresAtUtc", (object?)expiresAtUtc ?? DBNull.Value);
                p.AddWithValue("recipientCount", recipientCount);
                p.AddWithValue("attachmentFileName", (object?)attachmentFileName ?? DBNull.Value);
                p.AddWithValue("attachmentStoredFileName", (object?)attachmentStoredFileName ?? DBNull.Value);
                p.AddWithValue("attachmentContentType", (object?)attachmentContentType ?? DBNull.Value);
                p.AddWithValue("attachmentFileSize", (object?)attachmentFileSize ?? DBNull.Value);
            });

    public async Task<AnnouncementDto?> GetByIdAsync(int id, int schoolId) =>
        await db.QuerySingleOrDefaultAsync(
            $"""{SelectWithNames} WHERE a."Id" = @id AND a."SchoolId" = @schoolId""",
            p =>
            {
                p.AddWithValue("id", id);
                p.AddWithValue("schoolId", schoolId);
            },
            Map);

    public async Task<List<AnnouncementDto>> GetRecentAsync(int schoolId, int take) =>
        await db.QueryAsync(
            $"""{SelectWithNames} WHERE a."SchoolId" = @schoolId ORDER BY a."CreatedAtUtc" DESC LIMIT @take""",
            p =>
            {
                p.AddWithValue("schoolId", schoolId);
                p.AddWithValue("take", take);
            },
            Map);

    public async Task<AnnouncementDispatchItem?> GetDispatchItemAsync(int id, int schoolId) =>
        await db.QuerySingleOrDefaultAsync(
            """
            SELECT "Id", "SchoolId", "AuthorUserId", "Title", "Body", "ScopeType", "ScopeGradeId", "ScopeSectionId", "Priority", "Status", "ExpiresAtUtc"
            FROM "Announcements" WHERE "Id" = @id AND "SchoolId" = @schoolId
            """,
            p => { p.AddWithValue("id", id); p.AddWithValue("schoolId", schoolId); },
            MapDispatch);

    public async Task<List<AnnouncementDispatchItem>> GetDueScheduledAsync() =>
        await db.QueryAsync(
            """
            SELECT "Id", "SchoolId", "AuthorUserId", "Title", "Body", "ScopeType", "ScopeGradeId", "ScopeSectionId", "Priority", "Status", "ExpiresAtUtc"
            FROM "Announcements"
            WHERE "Status" = 'Scheduled' AND "ScheduledForUtc" <= now() AND ("ExpiresAtUtc" IS NULL OR "ExpiresAtUtc" > now())
            ORDER BY "ScheduledForUtc"
            LIMIT 50
            """,
            bind: null,
            MapDispatch);

    public async Task MarkSentAsync(int id, int recipientCount) =>
        await db.ExecuteAsync(
            """UPDATE "Announcements" SET "Status" = 'Sent', "SentAtUtc" = now(), "RecipientCount" = @recipientCount, "DispatchAttempts" = "DispatchAttempts" + 1, "LastDispatchError" = NULL WHERE "Id" = @id AND "Status" IN ('Draft', 'Scheduled')""",
            p => { p.AddWithValue("id", id); p.AddWithValue("recipientCount", recipientCount); });

    public async Task SetLifecycleAsync(int id, string status, DateTime? scheduledForUtc = null) =>
        await db.ExecuteAsync(
            """
            UPDATE "Announcements"
            SET "Status" = @status,
                "ScheduledForUtc" = CASE WHEN @status = 'Scheduled' THEN CAST(@scheduledForUtc AS timestamp with time zone) ELSE "ScheduledForUtc" END,
                "WithdrawnAtUtc" = CASE WHEN @status = 'Withdrawn' THEN now() ELSE NULL END
            WHERE "Id" = @id
            """,
            p => { p.AddWithValue("id", id); p.AddWithValue("status", status); p.AddWithValue("scheduledForUtc", (object?)scheduledForUtc ?? DBNull.Value); });

    public async Task ExpireDueAsync() =>
        await db.ExecuteAsync(
            """UPDATE "Announcements" SET "Status" = 'Expired' WHERE "Status" IN ('Sent', 'Scheduled') AND "ExpiresAtUtc" <= now()""",
            bind: null);

    public async Task UpdateDraftAsync(int id, string title, string body, string priority, DateTime? expiresAtUtc) =>
        await db.ExecuteAsync(
            """UPDATE "Announcements" SET "Title" = @title, "Body" = @body, "Priority" = @priority, "ExpiresAtUtc" = @expiresAtUtc, "UpdatedAtUtc" = now() WHERE "Id" = @id AND "Status" = 'Draft'""",
            p => { p.AddWithValue("id", id); p.AddWithValue("title", title); p.AddWithValue("body", body); p.AddWithValue("priority", priority); p.AddWithValue("expiresAtUtc", (object?)expiresAtUtc ?? DBNull.Value); });

    public async Task MarkDispatchFailedAsync(int id, string error) =>
        await db.ExecuteAsync(
            """UPDATE "Announcements" SET "DispatchAttempts" = "DispatchAttempts" + 1, "LastDispatchError" = @error WHERE "Id" = @id""",
            p => { p.AddWithValue("id", id); p.AddWithValue("error", error.Length > 1000 ? error[..1000] : error); });

    public async Task RetryAsync(int id) =>
        await db.ExecuteAsync(
            """UPDATE "Announcements" SET "Status" = 'Scheduled', "ScheduledForUtc" = now(), "LastDispatchError" = NULL WHERE "Id" = @id AND "Status" NOT IN ('Withdrawn', 'Expired')""",
            p => p.AddWithValue("id", id));

    public sealed record AttachmentFile(string FileName, string StoredFileName, string ContentType);
    public async Task<AttachmentFile?> GetAttachmentAsync(int id, int schoolId, int userId, bool isStaff) =>
        await db.QuerySingleOrDefaultAsync(
            """
            SELECT a."AttachmentFileName", a."AttachmentStoredFileName", a."AttachmentContentType"
            FROM "Announcements" a
            WHERE a."Id" = @id AND a."SchoolId" = @schoolId AND a."AttachmentStoredFileName" IS NOT NULL
              AND (@isStaff OR EXISTS (SELECT 1 FROM "Notifications" n WHERE n."AnnouncementId" = a."Id" AND n."UserId" = @userId))
            """,
            p => { p.AddWithValue("id", id); p.AddWithValue("schoolId", schoolId); p.AddWithValue("userId", userId); p.AddWithValue("isStaff", isStaff); },
            reader => new AttachmentFile(reader.GetString("AttachmentFileName"), reader.GetString("AttachmentStoredFileName"), reader.GetString("AttachmentContentType")));

    private static AnnouncementDispatchItem MapDispatch(NpgsqlDataReader reader) => new(
        reader.GetInt32("Id"),
        reader.GetInt32("SchoolId"),
        reader.GetInt32("AuthorUserId"),
        reader.GetString("Title"),
        reader.GetString("Body"),
        reader.GetString("ScopeType"),
        reader.GetNullableInt32("ScopeGradeId"),
        reader.GetNullableInt32("ScopeSectionId"),
        reader.GetString("Priority"),
        reader.GetString("Status"),
        reader.GetNullableDateTimeUtc("ExpiresAtUtc"));
}
