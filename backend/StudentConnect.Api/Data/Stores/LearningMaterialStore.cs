using Npgsql;
using StudentConnect.Api.Models;

namespace StudentConnect.Api.Data.Stores;

public class LearningMaterialStore(Database db)
{
    private const string SelectFull = """
        SELECT m."Id", m."Title", m."Description", m."SubjectId", m."GradeId", m."TeacherId",
               m."FileName", m."StoredFileName", m."ContentType", m."FileSize", m."UploadedDate",
               sub."Name" AS "SubjectName", g."Name" AS "GradeName", t."FirstName" AS "TeacherFirstName", t."LastName" AS "TeacherLastName"
        FROM "LearningMaterials" m
        JOIN "Subjects" sub ON sub."Id" = m."SubjectId"
        JOIN "Grades" g ON g."Id" = m."GradeId"
        JOIN "Teachers" t ON t."Id" = m."TeacherId"
        """;

    private static LearningMaterial Map(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32("Id"),
        Title = reader.GetString("Title"),
        Description = reader.GetString("Description"),
        SubjectId = reader.GetInt32("SubjectId"),
        Subject = new Subject { Name = reader.GetString("SubjectName") },
        GradeId = reader.GetInt32("GradeId"),
        Grade = new Grade { Name = reader.GetString("GradeName") },
        TeacherId = reader.GetInt32("TeacherId"),
        Teacher = new Teacher { FirstName = reader.GetString("TeacherFirstName"), LastName = reader.GetString("TeacherLastName") },
        FileName = reader.GetString("FileName"),
        StoredFileName = reader.GetString("StoredFileName"),
        ContentType = reader.GetString("ContentType"),
        FileSize = reader.GetInt64("FileSize"),
        UploadedDate = reader.GetDateTimeUtc("UploadedDate"),
    };

    public async Task<List<LearningMaterial>> GetAllAsync(int? subjectId, int? gradeId)
    {
        var where = new List<string>();
        if (subjectId is not null)
        {
            where.Add("""m."SubjectId" = @subjectId""");
        }
        if (gradeId is not null)
        {
            where.Add("""m."GradeId" = @gradeId""");
        }
        var whereClause = where.Count > 0 ? $"WHERE {string.Join(" AND ", where)}" : "";

        return await db.QueryAsync(
            $"""{SelectFull} {whereClause} ORDER BY m."UploadedDate" DESC""",
            p =>
            {
                if (subjectId is not null) p.AddWithValue("subjectId", subjectId.Value);
                if (gradeId is not null) p.AddWithValue("gradeId", gradeId.Value);
            },
            Map);
    }

    public async Task<LearningMaterial?> GetByIdAsync(int id) =>
        await db.QuerySingleOrDefaultAsync($"""{SelectFull} WHERE m."Id" = @id""", p => p.AddWithValue("id", id), Map);

    public async Task<List<LearningMaterial>> GetByGradeAsync(int gradeId) =>
        await db.QueryAsync($"""{SelectFull} WHERE m."GradeId" = @gradeId ORDER BY m."UploadedDate" DESC""", p => p.AddWithValue("gradeId", gradeId), Map);

    public async Task<List<LearningMaterial>> GetBySubjectAsync(int subjectId) =>
        await db.QueryAsync($"""{SelectFull} WHERE m."SubjectId" = @subjectId ORDER BY m."UploadedDate" DESC""", p => p.AddWithValue("subjectId", subjectId), Map);

    public async Task<List<LearningMaterial>> GetByTeacherAsync(int teacherId) =>
        await db.QueryAsync($"""{SelectFull} WHERE m."TeacherId" = @teacherId ORDER BY m."UploadedDate" DESC""", p => p.AddWithValue("teacherId", teacherId), Map);

    public async Task<List<LearningMaterial>> GetForStudentAsync(int gradeId, IReadOnlyCollection<int> subjectIds)
    {
        if (subjectIds.Count == 0)
        {
            return [];
        }

        return await db.QueryAsync(
            $"""{SelectFull} WHERE m."GradeId" = @gradeId AND m."SubjectId" = ANY(@subjectIds) ORDER BY m."UploadedDate" DESC""",
            p =>
            {
                p.AddWithValue("gradeId", gradeId);
                p.AddWithValue("subjectIds", subjectIds.ToArray());
            },
            Map);
    }

    // Metadata only — the file bytes live in IFileStorage, referenced by StoredFileName.
    public async Task<string?> GetStoredFileNameAsync(int id) =>
        await db.ExecuteScalarAsync<string>("""SELECT "StoredFileName" FROM "LearningMaterials" WHERE "Id" = @id""", p => p.AddWithValue("id", id));

    public async Task<int> CreateAsync(LearningMaterial material) =>
        await db.ExecuteScalarAsync<int>(
            """
            INSERT INTO "LearningMaterials" ("Title", "Description", "SubjectId", "GradeId", "TeacherId", "FileName", "StoredFileName", "ContentType", "FileSize", "UploadedDate")
            VALUES (@title, @description, @subjectId, @gradeId, @teacherId, @fileName, @storedFileName, @contentType, @fileSize, @uploadedDate)
            RETURNING "Id"
            """,
            p =>
            {
                p.AddWithValue("title", material.Title);
                p.AddWithValue("description", material.Description);
                p.AddWithValue("subjectId", material.SubjectId);
                p.AddWithValue("gradeId", material.GradeId);
                p.AddWithValue("teacherId", material.TeacherId);
                p.AddWithValue("fileName", material.FileName);
                p.AddWithValue("storedFileName", material.StoredFileName);
                p.AddWithValue("contentType", material.ContentType);
                p.AddWithValue("fileSize", material.FileSize);
                p.AddWithValue("uploadedDate", material.UploadedDate);
            });

    public async Task DeleteAsync(int id) =>
        await db.ExecuteAsync("""DELETE FROM "LearningMaterials" WHERE "Id" = @id""", p => p.AddWithValue("id", id));
}
