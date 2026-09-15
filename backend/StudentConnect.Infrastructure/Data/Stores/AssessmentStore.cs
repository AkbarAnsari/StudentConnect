using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;

namespace StudentConnect.Infrastructure.Data.Stores;

public class AssessmentStore(Database db)
{
    private const string SelectWithSubject = """
        SELECT a."Id", a."Title", a."SubjectId", a."Date", a."MaximumMarks", a."AssessmentType", a."Description", a."TermId", a."Weight",
               sub."Name" AS "SubjectName", term."Name" AS "TermName"
        FROM "Assessments" a
        JOIN "Subjects" sub ON sub."Id" = a."SubjectId"
        LEFT JOIN "Terms" term ON term."Id" = a."TermId"
        """;

    private static Assessment Map(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32("Id"),
        Title = reader.GetString("Title"),
        SubjectId = reader.GetInt32("SubjectId"),
        Subject = new Subject { Name = reader.GetString("SubjectName") },
        Date = reader.GetDateOnly("Date"),
        MaximumMarks = reader.GetInt32("MaximumMarks"),
        AssessmentType = reader.GetEnum<AssessmentType>("AssessmentType"),
        Description = reader.GetString("Description"),
        TermId = reader.GetNullableInt32("TermId"),
        Term = reader.GetNullableInt32("TermId") is not null ? new Term { Name = reader.GetString("TermName") } : null,
        Weight = reader.GetDecimal("Weight"),
    };

    public async Task<List<Assessment>> GetAllAsync() =>
        await db.QueryAsync($"""{SelectWithSubject} ORDER BY a."SubjectId", a."Date" """, bind: null, map: Map);

    // Tenant-scoped transitively through Subject (Assessments have no direct SchoolId column).
    public async Task<List<Assessment>> GetAllAsync(int schoolId) =>
        await db.QueryAsync(
            $"""{SelectWithSubject} WHERE sub."SchoolId" = @schoolId ORDER BY a."SubjectId", a."Date" """,
            p => p.AddWithValue("schoolId", schoolId),
            Map);

    public async Task<Assessment?> GetByIdAsync(int id) =>
        await db.QuerySingleOrDefaultAsync($"""{SelectWithSubject} WHERE a."Id" = @id""", p => p.AddWithValue("id", id), Map);

    public async Task<Assessment?> GetByIdAsync(int id, int schoolId) =>
        await db.QuerySingleOrDefaultAsync(
            $"""{SelectWithSubject} WHERE a."Id" = @id AND sub."SchoolId" = @schoolId""",
            p =>
            {
                p.AddWithValue("id", id);
                p.AddWithValue("schoolId", schoolId);
            },
            Map);

    public async Task<List<Assessment>> GetBySubjectAsync(int subjectId) =>
        await db.QueryAsync(
            $"""{SelectWithSubject} WHERE a."SubjectId" = @subjectId ORDER BY a."Date" """,
            p => p.AddWithValue("subjectId", subjectId),
            Map);

    public async Task<List<Assessment>> GetBySubjectAsync(int subjectId, int schoolId) =>
        await db.QueryAsync(
            $"""{SelectWithSubject} WHERE a."SubjectId" = @subjectId AND sub."SchoolId" = @schoolId ORDER BY a."Date" """,
            p =>
            {
                p.AddWithValue("subjectId", subjectId);
                p.AddWithValue("schoolId", schoolId);
            },
            Map);

    public async Task<bool> ExistsAsync(int id) =>
        await db.ExistsAsync("""SELECT 1 FROM "Assessments" WHERE "Id" = @id""", p => p.AddWithValue("id", id));

    public async Task<bool> ExistsAsync(int id, int schoolId) =>
        await db.ExistsAsync(
            """
            SELECT 1 FROM "Assessments" a JOIN "Subjects" sub ON sub."Id" = a."SubjectId"
            WHERE a."Id" = @id AND sub."SchoolId" = @schoolId
            """,
            p =>
            {
                p.AddWithValue("id", id);
                p.AddWithValue("schoolId", schoolId);
            });

    public async Task<int> CreateAsync(Assessment assessment) =>
        await db.ExecuteScalarAsync<int>(
            """
            INSERT INTO "Assessments" ("Title", "SubjectId", "Date", "MaximumMarks", "AssessmentType", "Description", "TermId", "Weight")
            VALUES (@title, @subjectId, @date, @maxMarks, @type, @description, @termId, @weight)
            RETURNING "Id"
            """,
            p => Bind(p, assessment));

    public async Task UpdateAsync(Assessment assessment) =>
        await db.ExecuteAsync(
            """
            UPDATE "Assessments"
            SET "Title" = @title, "SubjectId" = @subjectId, "Date" = @date, "MaximumMarks" = @maxMarks,
                "AssessmentType" = @type, "Description" = @description, "TermId" = @termId, "Weight" = @weight
            WHERE "Id" = @id
            """,
            p =>
            {
                p.AddWithValue("id", assessment.Id);
                Bind(p, assessment);
            });

    // Throws PostgresException (SqlState 23503) if marks still reference this assessment —
    // the controller catches that and returns 409 Conflict, same as the old DbUpdateException path.
    public async Task DeleteAsync(int id) =>
        await db.ExecuteAsync("""DELETE FROM "Assessments" WHERE "Id" = @id""", p => p.AddWithValue("id", id));

    private static void Bind(NpgsqlParameterCollection p, Assessment assessment)
    {
        p.AddWithValue("title", assessment.Title);
        p.AddWithValue("subjectId", assessment.SubjectId);
        p.AddWithValue("date", assessment.Date);
        p.AddWithValue("maxMarks", assessment.MaximumMarks);
        p.AddWithValue("type", assessment.AssessmentType.ToString());
        p.AddWithValue("description", assessment.Description);
        p.AddWithValue("termId", (object?)assessment.TermId ?? DBNull.Value);
        p.AddWithValue("weight", assessment.Weight);
    }
}
