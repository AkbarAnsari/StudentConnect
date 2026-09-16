using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;

namespace StudentConnect.Infrastructure.Data.Stores;

public class LearningMaterialStore(Database db)
{
    private const string SelectFull = """
           SELECT m."Id", m."Title", m."Description", m."Topic", m."SubjectId", m."GradeId", m."SectionId", m."TeacherId",
               m."FileName", m."StoredFileName", m."ContentType", m."FileSize", m."UploadedDate",
               m."Version", m."Status", m."ScheduledForUtc", m."PublishedAtUtc", m."ClosedAtUtc", m."ArchivedAtUtc",
             sub."Name" AS "SubjectName", g."Name" AS "GradeName", g."SchoolId", sec."Name" AS "SectionName",
             t."FirstName" AS "TeacherFirstName", t."LastName" AS "TeacherLastName"
        FROM "LearningMaterials" m
        JOIN "Subjects" sub ON sub."Id" = m."SubjectId"
        JOIN "Grades" g ON g."Id" = m."GradeId"
        LEFT JOIN "Sections" sec ON sec."Id" = m."SectionId"
        JOIN "Teachers" t ON t."Id" = m."TeacherId"
        """;

    private static LearningMaterial Map(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32("Id"),
        Title = reader.GetString("Title"),
        Description = reader.GetString("Description"),
        Topic = reader.GetString("Topic"),
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
        FileName = reader.GetString("FileName"),
        StoredFileName = reader.GetString("StoredFileName"),
        ContentType = reader.GetString("ContentType"),
        FileSize = reader.GetInt64("FileSize"),
        UploadedDate = reader.GetDateTimeUtc("UploadedDate"),
        Version = reader.GetInt32("Version"),
        Status = reader.GetEnum<ContentLifecycleStatus>("Status"),
        ScheduledForUtc = reader.GetNullableDateTimeUtc("ScheduledForUtc"),
        PublishedAtUtc = reader.GetNullableDateTimeUtc("PublishedAtUtc"),
        ClosedAtUtc = reader.GetNullableDateTimeUtc("ClosedAtUtc"),
        ArchivedAtUtc = reader.GetNullableDateTimeUtc("ArchivedAtUtc"),
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

    public async Task<List<LearningMaterial>> GetAllAsync(int schoolId, int? subjectId, int? gradeId)
    {
        var where = new List<string> { """g."SchoolId" = @schoolId""" };
        if (subjectId is not null)
        {
            where.Add("""m."SubjectId" = @subjectId""");
        }
        if (gradeId is not null)
        {
            where.Add("""m."GradeId" = @gradeId""");
        }

        return await db.QueryAsync(
            $"""{SelectFull} WHERE {string.Join(" AND ", where)} ORDER BY m."UploadedDate" DESC""",
            p =>
            {
                p.AddWithValue("schoolId", schoolId);
                if (subjectId is not null) p.AddWithValue("subjectId", subjectId.Value);
                if (gradeId is not null) p.AddWithValue("gradeId", gradeId.Value);
            },
            Map);
    }

    public async Task<LearningMaterial?> GetByIdAsync(int id) =>
        await db.QuerySingleOrDefaultAsync($"""{SelectFull} WHERE m."Id" = @id""", p => p.AddWithValue("id", id), Map);

    public async Task<LearningMaterial?> GetByIdAsync(int id, int schoolId) =>
        await db.QuerySingleOrDefaultAsync(
            $"""{SelectFull} WHERE m."Id" = @id AND g."SchoolId" = @schoolId""",
            p =>
            {
                p.AddWithValue("id", id);
                p.AddWithValue("schoolId", schoolId);
            },
            Map);

    public async Task<List<LearningMaterial>> GetByGradeAsync(int gradeId) =>
        await db.QueryAsync($"""{SelectFull} WHERE m."GradeId" = @gradeId ORDER BY m."UploadedDate" DESC""", p => p.AddWithValue("gradeId", gradeId), Map);

    public async Task<List<LearningMaterial>> GetByGradeAsync(int gradeId, int schoolId) =>
        await db.QueryAsync(
            $"""{SelectFull} WHERE m."GradeId" = @gradeId AND g."SchoolId" = @schoolId ORDER BY m."UploadedDate" DESC""",
            p =>
            {
                p.AddWithValue("gradeId", gradeId);
                p.AddWithValue("schoolId", schoolId);
            },
            Map);

    public async Task<List<LearningMaterial>> GetBySubjectAsync(int subjectId) =>
        await db.QueryAsync($"""{SelectFull} WHERE m."SubjectId" = @subjectId ORDER BY m."UploadedDate" DESC""", p => p.AddWithValue("subjectId", subjectId), Map);

    public async Task<List<LearningMaterial>> GetBySubjectAsync(int subjectId, int schoolId) =>
        await db.QueryAsync(
            $"""{SelectFull} WHERE m."SubjectId" = @subjectId AND g."SchoolId" = @schoolId ORDER BY m."UploadedDate" DESC""",
            p =>
            {
                p.AddWithValue("subjectId", subjectId);
                p.AddWithValue("schoolId", schoolId);
            },
            Map);

    public async Task<List<LearningMaterial>> GetByTeacherAsync(int teacherId) =>
        await db.QueryAsync($"""{SelectFull} WHERE m."TeacherId" = @teacherId ORDER BY m."UploadedDate" DESC""", p => p.AddWithValue("teacherId", teacherId), Map);

    public async Task<List<LearningMaterial>> GetByTeacherAsync(int teacherId, int schoolId) =>
        await db.QueryAsync(
            $"""{SelectFull} WHERE m."TeacherId" = @teacherId AND g."SchoolId" = @schoolId ORDER BY m."UploadedDate" DESC""",
            p =>
            {
                p.AddWithValue("teacherId", teacherId);
                p.AddWithValue("schoolId", schoolId);
            },
            Map);

    public async Task<List<LearningMaterial>> GetDueScheduledAsync() =>
        await db.QueryAsync(
            $"""{SelectFull} WHERE m."Status" = 'Scheduled' AND m."ScheduledForUtc" <= now() ORDER BY m."ScheduledForUtc" """,
            bind: null,
            Map);

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

    public async Task<List<LearningMaterial>> GetForStudentAsync(int gradeId, int? sectionId, IReadOnlyCollection<int> subjectIds, int schoolId)
    {
        if (subjectIds.Count == 0)
        {
            return [];
        }

        return await db.QueryAsync(
            $"""{SelectFull} WHERE m."GradeId" = @gradeId AND (m."SectionId" IS NULL OR m."SectionId" = @sectionId) AND m."SubjectId" = ANY(@subjectIds) AND g."SchoolId" = @schoolId AND (m."Status" = 'Published' OR (m."Status" = 'Scheduled' AND m."ScheduledForUtc" <= now())) ORDER BY m."UploadedDate" DESC""",
            p =>
            {
                p.AddWithValue("gradeId", gradeId);
                p.AddWithValue("sectionId", (object?)sectionId ?? DBNull.Value);
                p.AddWithValue("subjectIds", subjectIds.ToArray());
                p.AddWithValue("schoolId", schoolId);
            },
            Map);
    }

    // Metadata only — the file bytes live in IFileStorage, referenced by StoredFileName.
    public async Task<string?> GetStoredFileNameAsync(int id) =>
        await db.ExecuteScalarAsync<string>("""SELECT "StoredFileName" FROM "LearningMaterials" WHERE "Id" = @id""", p => p.AddWithValue("id", id));

    public async Task<int> CreateAsync(LearningMaterial material, int? actorUserId = null) =>
        await db.WithTransactionAsync(async (connection, transaction) =>
        {
            await using var materialCommand = new NpgsqlCommand(
                """
                INSERT INTO "LearningMaterials" ("Title", "Description", "Topic", "SubjectId", "GradeId", "SectionId", "TeacherId", "FileName", "StoredFileName", "ContentType", "FileSize", "UploadedDate", "Version", "Status", "ScheduledForUtc", "PublishedAtUtc")
                VALUES (@title, @description, @topic, @subjectId, @gradeId, @sectionId, @teacherId, @fileName, @storedFileName, @contentType, @fileSize, @uploadedDate, 1, @status, @scheduledForUtc, @publishedAtUtc)
                RETURNING "Id"
                """,
                connection,
                transaction);
            materialCommand.Parameters.AddWithValue("title", material.Title);
            materialCommand.Parameters.AddWithValue("description", material.Description);
            materialCommand.Parameters.AddWithValue("topic", material.Topic);
            materialCommand.Parameters.AddWithValue("subjectId", material.SubjectId);
            materialCommand.Parameters.AddWithValue("gradeId", material.GradeId);
            materialCommand.Parameters.AddWithValue("sectionId", (object?)material.SectionId ?? DBNull.Value);
            materialCommand.Parameters.AddWithValue("teacherId", material.TeacherId);
            materialCommand.Parameters.AddWithValue("fileName", material.FileName);
            materialCommand.Parameters.AddWithValue("storedFileName", material.StoredFileName);
            materialCommand.Parameters.AddWithValue("contentType", material.ContentType);
            materialCommand.Parameters.AddWithValue("fileSize", material.FileSize);
            materialCommand.Parameters.AddWithValue("uploadedDate", material.UploadedDate);
            materialCommand.Parameters.AddWithValue("status", material.Status.ToString());
            materialCommand.Parameters.AddWithValue("scheduledForUtc", (object?)material.ScheduledForUtc ?? DBNull.Value);
            materialCommand.Parameters.AddWithValue("publishedAtUtc", (object?)material.PublishedAtUtc ?? DBNull.Value);
            var materialId = Convert.ToInt32(await materialCommand.ExecuteScalarAsync());

            await InsertVersionAsync(connection, transaction, materialId, 1, material.FileName, material.StoredFileName,
                material.ContentType, material.FileSize, material.UploadedDate, actorUserId, "Initial upload");
            return materialId;
        });

    public async Task<List<LearningMaterialVersionDto>> GetVersionsAsync(int materialId, int schoolId) =>
        await db.QueryAsync(
            """
            SELECT v."Id", v."Version", v."FileName", v."ContentType", v."FileSize", v."UploadedAtUtc", v."ChangeReason",
                   COALESCE(NULLIF(concat_ws(' ', st."FirstName", st."LastName"), ''), NULLIF(concat_ws(' ', t."FirstName", t."LastName"), ''), u."Username") AS "UploadedByName"
            FROM "LearningMaterialVersions" v
            JOIN "LearningMaterials" m ON m."Id" = v."LearningMaterialId"
            JOIN "Grades" g ON g."Id" = m."GradeId"
            LEFT JOIN "Users" u ON u."Id" = v."UploadedByUserId"
            LEFT JOIN "Students" st ON st."Id" = u."StudentId"
            LEFT JOIN "Teachers" t ON t."Id" = u."TeacherId"
            WHERE v."LearningMaterialId" = @materialId AND g."SchoolId" = @schoolId
            ORDER BY v."Version" DESC
            """,
            p => { p.AddWithValue("materialId", materialId); p.AddWithValue("schoolId", schoolId); },
            reader => new LearningMaterialVersionDto
            {
                Id = reader.GetInt64("Id"), Version = reader.GetInt32("Version"), FileName = reader.GetString("FileName"),
                ContentType = reader.GetString("ContentType"), FileSize = reader.GetInt64("FileSize"),
                UploadedAtUtc = reader.GetDateTimeUtc("UploadedAtUtc"), UploadedByName = reader.GetNullableString("UploadedByName"),
                ChangeReason = reader.GetNullableString("ChangeReason"),
            });

    public sealed record VersionFile(string FileName, string StoredFileName, string ContentType);

    public async Task<VersionFile?> GetVersionFileAsync(int materialId, long versionId, int schoolId) =>
        await db.QuerySingleOrDefaultAsync(
            """
            SELECT v."FileName", v."StoredFileName", v."ContentType"
            FROM "LearningMaterialVersions" v
            JOIN "LearningMaterials" m ON m."Id" = v."LearningMaterialId"
            JOIN "Grades" g ON g."Id" = m."GradeId"
            WHERE v."Id" = @versionId AND v."LearningMaterialId" = @materialId AND g."SchoolId" = @schoolId
            """,
            p => { p.AddWithValue("versionId", versionId); p.AddWithValue("materialId", materialId); p.AddWithValue("schoolId", schoolId); },
            reader => new VersionFile(reader.GetString("FileName"), reader.GetString("StoredFileName"), reader.GetString("ContentType")));

    public async Task<int> ReplaceFileAsync(int id, string fileName, string storedFileName, string contentType, long fileSize, string reason, int? actorUserId) =>
        await db.WithTransactionAsync(async (connection, transaction) =>
        {
            await using var versionCommand = new NpgsqlCommand("""SELECT "Version" FROM "LearningMaterials" WHERE "Id" = @id FOR UPDATE""", connection, transaction);
            versionCommand.Parameters.AddWithValue("id", id);
            var currentVersion = Convert.ToInt32(await versionCommand.ExecuteScalarAsync());
            var version = currentVersion + 1;
            var uploadedAtUtc = DateTime.UtcNow;

            await using var updateCommand = new NpgsqlCommand(
                """UPDATE "LearningMaterials" SET "FileName" = @fileName, "StoredFileName" = @storedFileName, "ContentType" = @contentType, "FileSize" = @fileSize, "UploadedDate" = @uploadedAtUtc, "Version" = @version WHERE "Id" = @id""",
                connection,
                transaction);
            updateCommand.Parameters.AddWithValue("id", id);
            updateCommand.Parameters.AddWithValue("fileName", fileName);
            updateCommand.Parameters.AddWithValue("storedFileName", storedFileName);
            updateCommand.Parameters.AddWithValue("contentType", contentType);
            updateCommand.Parameters.AddWithValue("fileSize", fileSize);
            updateCommand.Parameters.AddWithValue("uploadedAtUtc", uploadedAtUtc);
            updateCommand.Parameters.AddWithValue("version", version);
            await updateCommand.ExecuteNonQueryAsync();

            await InsertVersionAsync(connection, transaction, id, version, fileName, storedFileName, contentType, fileSize, uploadedAtUtc, actorUserId, reason);
            return version;
        });

    public async Task<List<string>> GetStoredFileNamesAsync(int id) =>
        await db.QueryAsync(
            """SELECT DISTINCT "StoredFileName" FROM "LearningMaterialVersions" WHERE "LearningMaterialId" = @id""",
            p => p.AddWithValue("id", id),
            reader => reader.GetString("StoredFileName"));

    private static async Task InsertVersionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, int materialId, int version,
        string fileName, string storedFileName, string contentType, long fileSize, DateTime uploadedAtUtc, int? actorUserId, string? reason)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO "LearningMaterialVersions" ("LearningMaterialId", "Version", "FileName", "StoredFileName", "ContentType", "FileSize", "UploadedAtUtc", "UploadedByUserId", "ChangeReason")
            VALUES (@materialId, @version, @fileName, @storedFileName, @contentType, @fileSize, @uploadedAtUtc, @actorUserId, @reason)
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("materialId", materialId);
        command.Parameters.AddWithValue("version", version);
        command.Parameters.AddWithValue("fileName", fileName);
        command.Parameters.AddWithValue("storedFileName", storedFileName);
        command.Parameters.AddWithValue("contentType", contentType);
        command.Parameters.AddWithValue("fileSize", fileSize);
        command.Parameters.AddWithValue("uploadedAtUtc", uploadedAtUtc);
        command.Parameters.AddWithValue("actorUserId", (object?)actorUserId ?? DBNull.Value);
        command.Parameters.AddWithValue("reason", (object?)reason ?? DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }

    public async Task SetLifecycleAsync(int id, ContentLifecycleStatus status, DateTime? scheduledForUtc = null) =>
        await db.ExecuteAsync(
            """
            UPDATE "LearningMaterials"
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

    public async Task DeleteAsync(int id) =>
        await db.ExecuteAsync("""DELETE FROM "LearningMaterials" WHERE "Id" = @id""", p => p.AddWithValue("id", id));
}
