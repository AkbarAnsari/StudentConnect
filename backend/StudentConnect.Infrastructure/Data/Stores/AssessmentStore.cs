using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;

namespace StudentConnect.Infrastructure.Data.Stores;

public class AssessmentStore(Database db)
{
    private const string SelectWithSubject = """
        SELECT a."Id", a."Title", a."SubjectId", a."Date", a."MaximumMarks", a."AssessmentType", a."Description", sub."Name" AS "SubjectName"
        FROM "Assessments" a
        JOIN "Subjects" sub ON sub."Id" = a."SubjectId"
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
    };

    public async Task<List<Assessment>> GetAllAsync() =>
        await db.QueryAsync($"""{SelectWithSubject} ORDER BY a."SubjectId", a."Date" """, bind: null, map: Map);

    public async Task<Assessment?> GetByIdAsync(int id) =>
        await db.QuerySingleOrDefaultAsync($"""{SelectWithSubject} WHERE a."Id" = @id""", p => p.AddWithValue("id", id), Map);

    public async Task<List<Assessment>> GetBySubjectAsync(int subjectId) =>
        await db.QueryAsync(
            $"""{SelectWithSubject} WHERE a."SubjectId" = @subjectId ORDER BY a."Date" """,
            p => p.AddWithValue("subjectId", subjectId),
            Map);

    public async Task<bool> ExistsAsync(int id) =>
        await db.ExistsAsync("""SELECT 1 FROM "Assessments" WHERE "Id" = @id""", p => p.AddWithValue("id", id));

    public async Task<int> CreateAsync(Assessment assessment) =>
        await db.ExecuteScalarAsync<int>(
            """
            INSERT INTO "Assessments" ("Title", "SubjectId", "Date", "MaximumMarks", "AssessmentType", "Description")
            VALUES (@title, @subjectId, @date, @maxMarks, @type, @description)
            RETURNING "Id"
            """,
            p => Bind(p, assessment));

    public async Task UpdateAsync(Assessment assessment) =>
        await db.ExecuteAsync(
            """
            UPDATE "Assessments"
            SET "Title" = @title, "SubjectId" = @subjectId, "Date" = @date, "MaximumMarks" = @maxMarks,
                "AssessmentType" = @type, "Description" = @description
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
    }
}
