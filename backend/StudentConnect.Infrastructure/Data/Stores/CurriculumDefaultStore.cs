using Npgsql;

namespace StudentConnect.Infrastructure.Data.Stores;

public class CurriculumDefaultStore(Database db)
{
    public async Task<List<int>> GetSubjectIdsAsync(int gradeId, int? sectionId) =>
        await db.QueryAsync(
            """SELECT "SubjectId" FROM "CurriculumSubjectDefaults" WHERE "GradeId" = @gradeId AND "SectionId" IS NOT DISTINCT FROM @sectionId ORDER BY "SubjectId" """,
            p => { p.AddWithValue("gradeId", gradeId); p.AddWithValue("sectionId", (object?)sectionId ?? DBNull.Value); },
            reader => reader.GetInt32("SubjectId"));

    public async Task<List<int>> GetResolvedSubjectIdsAsync(int gradeId, int? sectionId) =>
        await db.QueryAsync(
            """
            SELECT DISTINCT "SubjectId" FROM "CurriculumSubjectDefaults"
            WHERE "GradeId" = @gradeId AND ("SectionId" IS NULL OR "SectionId" = @sectionId)
            ORDER BY "SubjectId"
            """,
            p => { p.AddWithValue("gradeId", gradeId); p.AddWithValue("sectionId", (object?)sectionId ?? DBNull.Value); },
            reader => reader.GetInt32("SubjectId"));

    public async Task ReplaceAsync(int gradeId, int? sectionId, IReadOnlyCollection<int> subjectIds) =>
        await db.WithTransactionAsync(async (connection, transaction) =>
        {
            await using var delete = new NpgsqlCommand(
                """DELETE FROM "CurriculumSubjectDefaults" WHERE "GradeId" = @gradeId AND "SectionId" IS NOT DISTINCT FROM @sectionId""",
                connection,
                transaction);
            delete.Parameters.AddWithValue("gradeId", gradeId);
            delete.Parameters.AddWithValue("sectionId", (object?)sectionId ?? DBNull.Value);
            await delete.ExecuteNonQueryAsync();

            if (subjectIds.Count > 0)
            {
                await using var insert = new NpgsqlCommand(
                    """INSERT INTO "CurriculumSubjectDefaults" ("GradeId", "SectionId", "SubjectId") SELECT @gradeId, @sectionId, unnest(@subjectIds)""",
                    connection,
                    transaction);
                insert.Parameters.AddWithValue("gradeId", gradeId);
                insert.Parameters.AddWithValue("sectionId", (object?)sectionId ?? DBNull.Value);
                insert.Parameters.AddWithValue("subjectIds", subjectIds.Distinct().ToArray());
                await insert.ExecuteNonQueryAsync();
            }
            return 0;
        });
}