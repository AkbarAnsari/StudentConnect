using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;

namespace StudentConnect.Infrastructure.Data.Stores;

public class MarkStore(Database db)
{
    private const string SelectWithAssessment = """
        SELECT m."Id", m."StudentId", m."AssessmentId", m."MarksObtained",
               asm."Title" AS "AssessmentTitle", asm."SubjectId" AS "AssessmentSubjectId", asm."Date" AS "AssessmentDate",
               asm."MaximumMarks" AS "AssessmentMaximumMarks", asm."TermId" AS "AssessmentTermId",
               sub."Name" AS "SubjectName"
        FROM "Marks" m
        JOIN "Assessments" asm ON asm."Id" = m."AssessmentId"
        JOIN "Subjects" sub ON sub."Id" = asm."SubjectId"
        """;

    private static Mark Map(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32("Id"),
        StudentId = reader.GetInt32("StudentId"),
        AssessmentId = reader.GetInt32("AssessmentId"),
        MarksObtained = reader.GetInt32("MarksObtained"),
        Assessment = new Assessment
        {
            Title = reader.GetString("AssessmentTitle"),
            SubjectId = reader.GetInt32("AssessmentSubjectId"),
            Date = reader.GetDateOnly("AssessmentDate"),
            MaximumMarks = reader.GetInt32("AssessmentMaximumMarks"),
            TermId = reader.GetNullableInt32("AssessmentTermId"),
            Subject = new Subject { Name = reader.GetString("SubjectName") },
        },
    };

    public async Task<List<Mark>> GetByStudentAsync(int studentId) =>
        await db.QueryAsync(
            $"""{SelectWithAssessment} WHERE m."StudentId" = @studentId ORDER BY asm."Date" DESC""",
            p => p.AddWithValue("studentId", studentId),
            Map);

    public async Task<List<Mark>> GetByStudentAndSubjectAsync(int studentId, int subjectId) =>
        await db.QueryAsync(
            $"""{SelectWithAssessment} WHERE m."StudentId" = @studentId AND asm."SubjectId" = @subjectId ORDER BY asm."Date" DESC""",
            p =>
            {
                p.AddWithValue("studentId", studentId);
                p.AddWithValue("subjectId", subjectId);
            },
            Map);

    public async Task<Mark?> GetByIdAsync(int id) =>
        await db.QuerySingleOrDefaultAsync($"""{SelectWithAssessment} WHERE m."Id" = @id""", p => p.AddWithValue("id", id), Map);

    public async Task<List<AssessmentResultDto>> GetAssessmentResultsAsync(int assessmentId) =>
        await db.QueryAsync(
            """
            SELECT m."Id" AS "MarkId", m."StudentId", st."FirstName", st."LastName", m."MarksObtained", asm."MaximumMarks"
            FROM "Marks" m
            JOIN "Students" st ON st."Id" = m."StudentId"
            JOIN "Assessments" asm ON asm."Id" = m."AssessmentId"
            WHERE m."AssessmentId" = @assessmentId
            ORDER BY m."MarksObtained" DESC
            """,
            p => p.AddWithValue("assessmentId", assessmentId),
            reader => new AssessmentResultDto
            {
                MarkId = reader.GetInt32("MarkId"),
                StudentId = reader.GetInt32("StudentId"),
                StudentName = $"{reader.GetString("FirstName")} {reader.GetString("LastName")}",
                MarksObtained = reader.GetInt32("MarksObtained"),
                MaximumMarks = reader.GetInt32("MaximumMarks"),
            });

    public async Task<List<MarksSummaryDto>> GetSummaryAsync(int studentId)
    {
        // Weighted average: each mark contributes (marksObtained/maximumMarks) * assessment
        // weight; default Weight is 1.00 so unweighted assessments behave exactly as before.
        var summary = await db.QueryAsync(
            """
            SELECT asm."SubjectId", sub."Name" AS "SubjectName",
                   COUNT(m."Id") AS "Assessments",
                   COALESCE(SUM(m."MarksObtained"), 0) AS "TotalMarksObtained",
                   COALESCE(SUM(asm."MaximumMarks"), 0) AS "TotalMaximumMarks",
                   COALESCE(SUM((m."MarksObtained"::numeric / NULLIF(asm."MaximumMarks", 0)) * asm."Weight"), 0) AS "WeightedScore",
                   COALESCE(SUM(asm."Weight"), 0) AS "TotalWeight"
            FROM "Marks" m
            JOIN "Assessments" asm ON asm."Id" = m."AssessmentId"
            JOIN "Subjects" sub ON sub."Id" = asm."SubjectId"
            WHERE m."StudentId" = @studentId
            GROUP BY asm."SubjectId", sub."Name"
            ORDER BY asm."SubjectId"
            """,
            p => p.AddWithValue("studentId", studentId),
            reader => new MarksSummaryDto
            {
                SubjectId = reader.GetInt32("SubjectId"),
                SubjectName = reader.GetString("SubjectName"),
                Assessments = (int)reader.GetInt64("Assessments"),
                TotalMarksObtained = (int)reader.GetInt64("TotalMarksObtained"),
                TotalMaximumMarks = (int)reader.GetInt64("TotalMaximumMarks"),
                WeightedScore = reader.GetDecimal("WeightedScore"),
                TotalWeight = reader.GetDecimal("TotalWeight"),
            });

        foreach (var item in summary)
        {
            item.Percentage = item.TotalWeight == 0 ? 0 : Math.Round((double)(item.WeightedScore * 100 / item.TotalWeight), 0);
            item.ResultCategory = ResultCategory.From(item.Percentage);
        }

        return summary;
    }


    public async Task<bool> ExistsAsync(int studentId, int assessmentId) =>
        await db.ExistsAsync(
            """SELECT 1 FROM "Marks" WHERE "StudentId" = @studentId AND "AssessmentId" = @assessmentId""",
            p =>
            {
                p.AddWithValue("studentId", studentId);
                p.AddWithValue("assessmentId", assessmentId);
            });

    public async Task<int> CreateAsync(int studentId, int assessmentId, int marksObtained) =>
        await db.ExecuteScalarAsync<int>(
            """INSERT INTO "Marks" ("StudentId", "AssessmentId", "MarksObtained") VALUES (@studentId, @assessmentId, @marksObtained) RETURNING "Id" """,
            p =>
            {
                p.AddWithValue("studentId", studentId);
                p.AddWithValue("assessmentId", assessmentId);
                p.AddWithValue("marksObtained", marksObtained);
            });

    public async Task UpdateMarksObtainedAsync(int id, int marksObtained) =>
        await db.ExecuteAsync(
            """UPDATE "Marks" SET "MarksObtained" = @marksObtained WHERE "Id" = @id""",
            p =>
            {
                p.AddWithValue("id", id);
                p.AddWithValue("marksObtained", marksObtained);
            });

    // --- Transactional bulk-save support (mirrors ActivityRegistrationStore's *InTxAsync pattern) ---

    public async Task<int> CreateInTxAsync(NpgsqlConnection connection, NpgsqlTransaction tx, int studentId, int assessmentId, int marksObtained)
    {
        await using var command = new NpgsqlCommand(
            """INSERT INTO "Marks" ("StudentId", "AssessmentId", "MarksObtained") VALUES (@studentId, @assessmentId, @marksObtained) RETURNING "Id" """,
            connection, tx);
        command.Parameters.AddWithValue("studentId", studentId);
        command.Parameters.AddWithValue("assessmentId", assessmentId);
        command.Parameters.AddWithValue("marksObtained", marksObtained);
        return (int)(await command.ExecuteScalarAsync())!;
    }

    public async Task UpdateMarksObtainedInTxAsync(NpgsqlConnection connection, NpgsqlTransaction tx, int id, int marksObtained)
    {
        await using var command = new NpgsqlCommand(
            """UPDATE "Marks" SET "MarksObtained" = @marksObtained WHERE "Id" = @id""",
            connection, tx);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("marksObtained", marksObtained);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(int id) =>
        await db.ExecuteAsync("""DELETE FROM "Marks" WHERE "Id" = @id""", p => p.AddWithValue("id", id));

    // Bulk save support: existing (studentId -> markId) rows for an assessment, mirroring the
    // original EF version's update-in-place-vs-insert logic.
    public async Task<Dictionary<int, int>> GetExistingIdsForAssessmentAsync(int assessmentId, IReadOnlyCollection<int> studentIds)
    {
        if (studentIds.Count == 0)
        {
            return [];
        }

        var rows = await db.QueryAsync(
            """SELECT "Id", "StudentId" FROM "Marks" WHERE "AssessmentId" = @assessmentId AND "StudentId" = ANY(@studentIds)""",
            p =>
            {
                p.AddWithValue("assessmentId", assessmentId);
                p.AddWithValue("studentIds", studentIds.ToArray());
            },
            reader => (StudentId: reader.GetInt32("StudentId"), Id: reader.GetInt32("Id")));

        return rows.ToDictionary(r => r.StudentId, r => r.Id);
    }
}
