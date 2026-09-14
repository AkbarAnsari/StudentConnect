using Npgsql;
using StudentConnect.Application;

namespace StudentConnect.Infrastructure.Data.Stores;

public class AnnouncementStore(Database db)
{
    private const string SelectWithNames = """
        SELECT a."Id", a."Title", a."Body", a."ScopeType", a."CreatedAtUtc",
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
    };

    public async Task<int> CreateAsync(int schoolId, int authorUserId, string title, string body, string scopeType, int? scopeGradeId, int? scopeSectionId) =>
        await db.ExecuteScalarAsync<int>(
            """
            INSERT INTO "Announcements" ("SchoolId", "AuthorUserId", "Title", "Body", "ScopeType", "ScopeGradeId", "ScopeSectionId")
            VALUES (@schoolId, @authorUserId, @title, @body, @scopeType, @scopeGradeId, @scopeSectionId)
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
}
