using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;

namespace StudentConnect.Infrastructure.Data.Stores;

public class HomeworkStore(Database db)
{
    private const string SelectFull = """
         SELECT h."Id", h."Title", h."Description", h."SubjectId", h."GradeId", h."SectionId", h."TeacherId", h."CreatedDate", h."DueDate",
             h."Status", h."ScheduledForUtc", h."PublishedAtUtc", h."ClosedAtUtc", h."ArchivedAtUtc",
             sub."Name" AS "SubjectName", g."Name" AS "GradeName", g."SchoolId", sec."Name" AS "SectionName",
             t."FirstName" AS "TeacherFirstName", t."LastName" AS "TeacherLastName"
        FROM "Homework" h
        JOIN "Subjects" sub ON sub."Id" = h."SubjectId"
        JOIN "Grades" g ON g."Id" = h."GradeId"
        LEFT JOIN "Sections" sec ON sec."Id" = h."SectionId"
        JOIN "Teachers" t ON t."Id" = h."TeacherId"
        """;

    private static Homework MapFull(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32("Id"),
        Title = reader.GetString("Title"),
        Description = reader.GetString("Description"),
        SubjectId = reader.GetInt32("SubjectId"),
        Subject = new Subject { Name = reader.GetString("SubjectName") },
        GradeId = reader.GetInt32("GradeId"),
        Grade = new Grade { Name = reader.GetString("GradeName"), SchoolId = reader.GetInt32("SchoolId") },
        SectionId = reader.GetNullableInt32("SectionId"),
        Section = reader.GetNullableInt32("SectionId") is { } sectionId
            ? new Section { Id = sectionId, Name = reader.GetNullableString("SectionName") ?? string.Empty }
            : null,
        TeacherId = reader.GetInt32("TeacherId"),
        Teacher = new Teacher { FirstName = reader.GetString("TeacherFirstName"), LastName = reader.GetString("TeacherLastName") },
        CreatedDate = reader.GetDateOnly("CreatedDate"),
        DueDate = reader.GetDateOnly("DueDate"),
        Status = reader.GetEnum<ContentLifecycleStatus>("Status"),
        ScheduledForUtc = reader.GetNullableDateTimeUtc("ScheduledForUtc"),
        PublishedAtUtc = reader.GetNullableDateTimeUtc("PublishedAtUtc"),
        ClosedAtUtc = reader.GetNullableDateTimeUtc("ClosedAtUtc"),
        ArchivedAtUtc = reader.GetNullableDateTimeUtc("ArchivedAtUtc"),
    };

    public async Task<List<Homework>> GetAllAsync() =>
        await db.QueryAsync($"""{SelectFull} ORDER BY h."DueDate" """, bind: null, map: MapFull);

    public async Task<List<Homework>> GetAllAsync(int schoolId) =>
        await db.QueryAsync(
            $"""{SelectFull} WHERE g."SchoolId" = @schoolId ORDER BY h."DueDate" """,
            p => p.AddWithValue("schoolId", schoolId),
            MapFull);

    public async Task<Homework?> GetByIdAsync(int id) =>
        await db.QuerySingleOrDefaultAsync($"""{SelectFull} WHERE h."Id" = @id""", p => p.AddWithValue("id", id), MapFull);

    // Tenant-scoped transitively through Grade (Homework has no direct SchoolId column).
    public async Task<Homework?> GetByIdAsync(int id, int schoolId) =>
        await db.QuerySingleOrDefaultAsync(
            $"""{SelectFull} WHERE h."Id" = @id AND g."SchoolId" = @schoolId""",
            p =>
            {
                p.AddWithValue("id", id);
                p.AddWithValue("schoolId", schoolId);
            },
            MapFull);

    public async Task<List<Homework>> GetByTeacherAsync(int teacherId) =>
        await db.QueryAsync($"""{SelectFull} WHERE h."TeacherId" = @teacherId ORDER BY h."DueDate" """, p => p.AddWithValue("teacherId", teacherId), MapFull);

    public async Task<List<Homework>> GetByTeacherAsync(int teacherId, int schoolId) =>
        await db.QueryAsync(
            $"""{SelectFull} WHERE h."TeacherId" = @teacherId AND g."SchoolId" = @schoolId ORDER BY h."DueDate" """,
            p =>
            {
                p.AddWithValue("teacherId", teacherId);
                p.AddWithValue("schoolId", schoolId);
            },
            MapFull);

    public async Task<List<Homework>> GetBySubjectAsync(int subjectId) =>
        await db.QueryAsync($"""{SelectFull} WHERE h."SubjectId" = @subjectId ORDER BY h."DueDate" """, p => p.AddWithValue("subjectId", subjectId), MapFull);

    public async Task<List<Homework>> GetBySubjectAsync(int subjectId, int schoolId) =>
        await db.QueryAsync(
            $"""{SelectFull} WHERE h."SubjectId" = @subjectId AND g."SchoolId" = @schoolId ORDER BY h."DueDate" """,
            p =>
            {
                p.AddWithValue("subjectId", subjectId);
                p.AddWithValue("schoolId", schoolId);
            },
            MapFull);

    public async Task<List<Homework>> GetDueScheduledAsync() =>
        await db.QueryAsync(
            $"""{SelectFull} WHERE h."Status" = 'Scheduled' AND h."ScheduledForUtc" <= now() ORDER BY h."ScheduledForUtc" """,
            bind: null,
            MapFull);

    // Used by the student homework list — Subject only, no Grade/Teacher needed.
    public async Task<List<Homework>> GetByGradeAsync(int gradeId) =>
        await db.QueryAsync(
            """
            SELECT h."Id", h."Title", h."DueDate", h."Status", sub."Name" AS "SubjectName"
            FROM "Homework" h
            JOIN "Subjects" sub ON sub."Id" = h."SubjectId"
            WHERE h."GradeId" = @gradeId
            ORDER BY h."DueDate"
            """,
            p => p.AddWithValue("gradeId", gradeId),
            reader => new Homework
            {
                Id = reader.GetInt32("Id"),
                Title = reader.GetString("Title"),
                DueDate = reader.GetDateOnly("DueDate"),
                Subject = new Subject { Name = reader.GetString("SubjectName") },
            });

    public async Task<List<Homework>> GetByGradeAsync(int gradeId, int schoolId) =>
        await db.QueryAsync(
            """
            SELECT h."Id", h."Title", h."DueDate", h."Status", sub."Name" AS "SubjectName"
            FROM "Homework" h
            JOIN "Subjects" sub ON sub."Id" = h."SubjectId"
            JOIN "Grades" g ON g."Id" = h."GradeId"
            WHERE h."GradeId" = @gradeId AND g."SchoolId" = @schoolId
            ORDER BY h."DueDate"
            """,
            p =>
            {
                p.AddWithValue("gradeId", gradeId);
                p.AddWithValue("schoolId", schoolId);
            },
            reader => new Homework
            {
                Id = reader.GetInt32("Id"),
                Title = reader.GetString("Title"),
                DueDate = reader.GetDateOnly("DueDate"),
                Subject = new Subject { Name = reader.GetString("SubjectName") },
            });

    public async Task<List<Homework>> GetForStudentAsync(int gradeId, int? sectionId, int schoolId) =>
        await db.QueryAsync(
            """
            SELECT h."Id", h."Title", h."DueDate", h."Status", sub."Name" AS "SubjectName"
            FROM "Homework" h
            JOIN "Subjects" sub ON sub."Id" = h."SubjectId"
            JOIN "Grades" g ON g."Id" = h."GradeId"
            WHERE h."GradeId" = @gradeId
              AND g."SchoolId" = @schoolId
              AND (h."SectionId" IS NULL OR h."SectionId" = @sectionId)
              AND (h."Status" IN ('Published', 'Closed') OR (h."Status" = 'Scheduled' AND h."ScheduledForUtc" <= now()))
            ORDER BY h."DueDate"
            """,
            p =>
            {
                p.AddWithValue("gradeId", gradeId);
                p.AddWithValue("sectionId", (object?)sectionId ?? DBNull.Value);
                p.AddWithValue("schoolId", schoolId);
            },
            reader => new Homework
            {
                Id = reader.GetInt32("Id"),
                Title = reader.GetString("Title"),
                DueDate = reader.GetDateOnly("DueDate"),
                Status = reader.GetEnum<ContentLifecycleStatus>("Status"),
                Subject = new Subject { Name = reader.GetString("SubjectName") },
            });

    public async Task<bool> ExistsAsync(int id) =>
        await db.ExistsAsync("""SELECT 1 FROM "Homework" WHERE "Id" = @id""", p => p.AddWithValue("id", id));

    public async Task<int> CreateAsync(Homework homework) =>
        await db.ExecuteScalarAsync<int>(
            """
            INSERT INTO "Homework" ("Title", "Description", "SubjectId", "GradeId", "SectionId", "TeacherId", "CreatedDate", "DueDate", "Status", "ScheduledForUtc", "PublishedAtUtc")
            VALUES (@title, @description, @subjectId, @gradeId, @sectionId, @teacherId, @createdDate, @dueDate, @status, @scheduledForUtc, @publishedAtUtc)
            RETURNING "Id"
            """,
            p =>
            {
                p.AddWithValue("title", homework.Title);
                p.AddWithValue("description", homework.Description);
                p.AddWithValue("subjectId", homework.SubjectId);
                p.AddWithValue("gradeId", homework.GradeId);
                p.AddWithValue("sectionId", (object?)homework.SectionId ?? DBNull.Value);
                p.AddWithValue("teacherId", homework.TeacherId);
                p.AddWithValue("createdDate", homework.CreatedDate);
                p.AddWithValue("dueDate", homework.DueDate);
                p.AddWithValue("status", homework.Status.ToString());
                p.AddWithValue("scheduledForUtc", (object?)homework.ScheduledForUtc ?? DBNull.Value);
                p.AddWithValue("publishedAtUtc", (object?)homework.PublishedAtUtc ?? DBNull.Value);
            });

    public async Task UpdateAsync(int id, string title, string description, DateOnly dueDate) =>
        await db.ExecuteAsync(
            """UPDATE "Homework" SET "Title" = @title, "Description" = @description, "DueDate" = @dueDate WHERE "Id" = @id""",
            p =>
            {
                p.AddWithValue("id", id);
                p.AddWithValue("title", title);
                p.AddWithValue("description", description);
                p.AddWithValue("dueDate", dueDate);
            });

    public async Task SetLifecycleAsync(int id, ContentLifecycleStatus status, DateTime? scheduledForUtc = null) =>
        await db.ExecuteAsync(
            """
            UPDATE "Homework"
            SET "Status" = @status,
                "ScheduledForUtc" = CASE WHEN @status = 'Scheduled' THEN CAST(@scheduledForUtc AS timestamp with time zone) ELSE NULL END,
                "PublishedAtUtc" = CASE WHEN @status = 'Published' THEN COALESCE("PublishedAtUtc", now()) ELSE "PublishedAtUtc" END,
                "ClosedAtUtc" = CASE WHEN @status = 'Closed' THEN now() ELSE NULL END,
                "ArchivedAtUtc" = CASE WHEN @status = 'Archived' THEN now() ELSE NULL END
            WHERE "Id" = @id
            """,
            p =>
            {
                p.AddWithValue("id", id);
                p.AddWithValue("status", status.ToString());
                p.AddWithValue("scheduledForUtc", (object?)scheduledForUtc ?? DBNull.Value);
            });

    // Single statement, so it is atomic across the whole batch without needing an explicit transaction.
    public async Task<int> UpdateDueDatesAsync(IReadOnlyCollection<int> ids, DateOnly dueDate) =>
        ids.Count == 0
            ? 0
            : await db.ExecuteAsync(
                """UPDATE "Homework" SET "DueDate" = @dueDate WHERE "Id" = ANY(@ids)""",
                p =>
                {
                    p.AddWithValue("dueDate", dueDate);
                    p.AddWithValue("ids", ids.ToArray());
                });

    // Submissions cascade-delete at the DB level (see database/schema.sql) — no app-side cleanup needed.
    public async Task DeleteAsync(int id) =>
        await db.ExecuteAsync("""DELETE FROM "Homework" WHERE "Id" = @id""", p => p.AddWithValue("id", id));

    public async Task<List<int>> GetAllStudentIdsInGradeAsync(int gradeId) =>
        await db.QueryAsync(
            """SELECT "Id" FROM "Students" WHERE "GradeId" = @gradeId""",
            p => p.AddWithValue("gradeId", gradeId),
            reader => reader.GetInt32("Id"));

    public async Task<List<int>> GetAllStudentIdsInGradeAsync(int gradeId, int schoolId) =>
        await db.QueryAsync(
            """SELECT "Id" FROM "Students" WHERE "GradeId" = @gradeId AND "SchoolId" = @schoolId""",
            p =>
            {
                p.AddWithValue("gradeId", gradeId);
                p.AddWithValue("schoolId", schoolId);
            },
            reader => reader.GetInt32("Id"));
}
