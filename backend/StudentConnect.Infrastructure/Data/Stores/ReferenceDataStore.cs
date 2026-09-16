using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;

namespace StudentConnect.Infrastructure.Data.Stores;

// Grades/Subjects are simple lookup tables, now admin-manageable (create/delete) via
// GradesController/SubjectsController; Teachers have their own dedicated TeacherStore.
public class ReferenceDataStore(Database db)
{
    private static Grade MapGrade(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32("Id"),
        Name = reader.GetString("Name"),
        SchoolId = reader.GetNullableInt32("SchoolId") ?? 0,
        SequenceOrder = reader.GetNullableInt32("SequenceOrder"),
    };

    private static Subject MapSubject(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32("Id"),
        Name = reader.GetString("Name"),
        Code = reader.GetString("Code"),
        SchoolId = reader.GetNullableInt32("SchoolId") ?? 0,
    };

    // Bulk lookup used by StudentsController's /subjects endpoint — turns a StudentSubjects
    // join's raw SubjectId list into the actual Subject rows, scoped to the caller's school.
    public async Task<List<Subject>> GetSubjectsByIdsAsync(IReadOnlyCollection<int> ids, int schoolId) =>
        ids.Count == 0
            ? []
            : await db.QueryAsync(
                """SELECT "Id", "Name", "Code", "SchoolId" FROM "Subjects" WHERE "SchoolId" = @schoolId AND "Id" = ANY(@ids)""",
                p =>
                {
                    p.AddWithValue("schoolId", schoolId);
                    p.AddWithValue("ids", ids.ToArray());
                },
                MapSubject);

    private static Teacher MapTeacher(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32("Id"),
        TeacherId = reader.GetString("TeacherId"),
        FirstName = reader.GetString("FirstName"),
        LastName = reader.GetString("LastName"),
        Email = reader.GetString("Email"),
    };

    public async Task<List<Grade>> GetAllGradesAsync() =>
        await db.QueryAsync(
            """SELECT "Id", "Name", "SchoolId", "SequenceOrder" FROM "Grades" ORDER BY "Id" """,
            bind: null,
            map: MapGrade);

    // Tenant-scoped — used by the multi-tenancy-aware controllers (Grades, Students, Sections).
    public async Task<List<Grade>> GetAllGradesAsync(int schoolId) =>
        await db.QueryAsync(
            """SELECT "Id", "Name", "SchoolId", "SequenceOrder" FROM "Grades" WHERE "SchoolId" = @schoolId ORDER BY "Id" """,
            bind: p => p.AddWithValue("schoolId", schoolId),
            map: MapGrade);

    public async Task<Grade?> GetGradeByIdAsync(int id) =>
        await db.QuerySingleOrDefaultAsync(
            """SELECT "Id", "Name", "SchoolId", "SequenceOrder" FROM "Grades" WHERE "Id" = @id""",
            bind: p => p.AddWithValue("id", id),
            map: MapGrade);

    public async Task<Grade?> GetGradeByIdAsync(int id, int schoolId) =>
        await db.QuerySingleOrDefaultAsync(
            """SELECT "Id", "Name", "SchoolId", "SequenceOrder" FROM "Grades" WHERE "Id" = @id AND "SchoolId" = @schoolId""",
            bind: p =>
            {
                p.AddWithValue("id", id);
                p.AddWithValue("schoolId", schoolId);
            },
            map: MapGrade);

    public async Task<bool> GradeExistsAsync(int id) =>
        await db.ExistsAsync(
            """SELECT 1 FROM "Grades" WHERE "Id" = @id""",
            bind: p => p.AddWithValue("id", id));

    public async Task<bool> GradeExistsAsync(int id, int schoolId) =>
        await db.ExistsAsync(
            """SELECT 1 FROM "Grades" WHERE "Id" = @id AND "SchoolId" = @schoolId""",
            bind: p =>
            {
                p.AddWithValue("id", id);
                p.AddWithValue("schoolId", schoolId);
            });

    public async Task<bool> GradeNameExistsAsync(string name) =>
        await db.ExistsAsync(
            """SELECT 1 FROM "Grades" WHERE "Name" = @name""",
            bind: p => p.AddWithValue("name", name));

    public async Task<bool> GradeNameExistsAsync(string name, int schoolId) =>
        await db.ExistsAsync(
            """SELECT 1 FROM "Grades" WHERE "Name" = @name AND "SchoolId" = @schoolId""",
            bind: p =>
            {
                p.AddWithValue("name", name);
                p.AddWithValue("schoolId", schoolId);
            });

    public async Task<bool> GradeNameExistsAsync(string name, int schoolId, int excludingId) =>
        await db.ExistsAsync(
            """SELECT 1 FROM "Grades" WHERE "Name" = @name AND "SchoolId" = @schoolId AND "Id" <> @excludingId""",
            p => { p.AddWithValue("name", name); p.AddWithValue("schoolId", schoolId); p.AddWithValue("excludingId", excludingId); });

    public async Task<bool> SectionBelongsToGradeAsync(int sectionId, int gradeId, int schoolId) =>
        await db.ExistsAsync(
            """SELECT 1 FROM "Sections" s JOIN "Grades" g ON g."Id" = s."GradeId" WHERE s."Id" = @sectionId AND s."GradeId" = @gradeId AND g."SchoolId" = @schoolId""",
            p =>
            {
                p.AddWithValue("sectionId", sectionId);
                p.AddWithValue("gradeId", gradeId);
                p.AddWithValue("schoolId", schoolId);
            });

    public async Task<int> CreateGradeAsync(string name, int schoolId) =>
        await db.ExecuteScalarAsync<int>(
            """INSERT INTO "Grades" ("Name", "SchoolId") VALUES (@name, @schoolId) RETURNING "Id" """,
            p =>
            {
                p.AddWithValue("name", name);
                p.AddWithValue("schoolId", schoolId);
            });

    public async Task DeleteGradeAsync(int id) =>
        await db.ExecuteAsync("""DELETE FROM "Grades" WHERE "Id" = @id""", p => p.AddWithValue("id", id));

    public async Task UpdateGradeAsync(int id, string name, int schoolId) =>
        await db.ExecuteAsync(
            """UPDATE "Grades" SET "Name" = @name WHERE "Id" = @id AND "SchoolId" = @schoolId""",
            p => { p.AddWithValue("id", id); p.AddWithValue("name", name); p.AddWithValue("schoolId", schoolId); });

    public async Task<List<Subject>> GetAllSubjectsAsync() =>
        await db.QueryAsync(
            """SELECT "Id", "Name", "Code", "SchoolId" FROM "Subjects" ORDER BY "Id" """,
            bind: null,
            map: MapSubject);

    public async Task<List<Subject>> GetAllSubjectsAsync(int schoolId) =>
        await db.QueryAsync(
            """SELECT "Id", "Name", "Code", "SchoolId" FROM "Subjects" WHERE "SchoolId" = @schoolId ORDER BY "Id" """,
            bind: p => p.AddWithValue("schoolId", schoolId),
            map: MapSubject);

    public async Task<Subject?> GetSubjectByIdAsync(int id) =>
        await db.QuerySingleOrDefaultAsync(
            """SELECT "Id", "Name", "Code", "SchoolId" FROM "Subjects" WHERE "Id" = @id""",
            bind: p => p.AddWithValue("id", id),
            map: MapSubject);

    public async Task<Subject?> GetSubjectByIdAsync(int id, int schoolId) =>
        await db.QuerySingleOrDefaultAsync(
            """SELECT "Id", "Name", "Code", "SchoolId" FROM "Subjects" WHERE "Id" = @id AND "SchoolId" = @schoolId""",
            bind: p =>
            {
                p.AddWithValue("id", id);
                p.AddWithValue("schoolId", schoolId);
            },
            map: MapSubject);

    public async Task<bool> SubjectExistsAsync(int id) =>
        await db.ExistsAsync(
            """SELECT 1 FROM "Subjects" WHERE "Id" = @id""",
            bind: p => p.AddWithValue("id", id));

    public async Task<bool> SubjectExistsAsync(int id, int schoolId) =>
        await db.ExistsAsync(
            """SELECT 1 FROM "Subjects" WHERE "Id" = @id AND "SchoolId" = @schoolId""",
            bind: p =>
            {
                p.AddWithValue("id", id);
                p.AddWithValue("schoolId", schoolId);
            });

    public async Task<bool> SubjectCodeExistsAsync(string code) =>
        await db.ExistsAsync(
            """SELECT 1 FROM "Subjects" WHERE "Code" = @code""",
            bind: p => p.AddWithValue("code", code));

    public async Task<bool> SubjectCodeExistsAsync(string code, int schoolId) =>
        await db.ExistsAsync(
            """SELECT 1 FROM "Subjects" WHERE "Code" = @code AND "SchoolId" = @schoolId""",
            bind: p =>
            {
                p.AddWithValue("code", code);
                p.AddWithValue("schoolId", schoolId);
            });

    public async Task<bool> SubjectCodeExistsAsync(string code, int schoolId, int excludingId) =>
        await db.ExistsAsync(
            """SELECT 1 FROM "Subjects" WHERE "Code" = @code AND "SchoolId" = @schoolId AND "Id" <> @excludingId""",
            p => { p.AddWithValue("code", code); p.AddWithValue("schoolId", schoolId); p.AddWithValue("excludingId", excludingId); });

    public async Task<int> CreateSubjectAsync(string name, string code, int schoolId) =>
        await db.ExecuteScalarAsync<int>(
            """INSERT INTO "Subjects" ("Name", "Code", "SchoolId") VALUES (@name, @code, @schoolId) RETURNING "Id" """,
            p =>
            {
                p.AddWithValue("name", name);
                p.AddWithValue("code", code);
                p.AddWithValue("schoolId", schoolId);
            });

    public async Task DeleteSubjectAsync(int id) =>
        await db.ExecuteAsync("""DELETE FROM "Subjects" WHERE "Id" = @id""", p => p.AddWithValue("id", id));

    public async Task UpdateSubjectAsync(int id, string name, string code, int schoolId) =>
        await db.ExecuteAsync(
            """UPDATE "Subjects" SET "Name" = @name, "Code" = @code WHERE "Id" = @id AND "SchoolId" = @schoolId""",
            p => { p.AddWithValue("id", id); p.AddWithValue("name", name); p.AddWithValue("code", code); p.AddWithValue("schoolId", schoolId); });

    public async Task<bool> TeacherExistsAsync(int id) =>
        await db.ExistsAsync(
            """SELECT 1 FROM "Teachers" WHERE "Id" = @id""",
            bind: p => p.AddWithValue("id", id));

    public async Task<bool> TeacherExistsAsync(int id, int schoolId) =>
        await db.ExistsAsync(
            """SELECT 1 FROM "Teachers" WHERE "Id" = @id AND "SchoolId" = @schoolId""",
            bind: p =>
            {
                p.AddWithValue("id", id);
                p.AddWithValue("schoolId", schoolId);
            });

    public async Task<Teacher?> GetTeacherByIdAsync(int id) =>
        await db.QuerySingleOrDefaultAsync(
            """SELECT "Id", "TeacherId", "FirstName", "LastName", "Email" FROM "Teachers" WHERE "Id" = @id""",
            bind: p => p.AddWithValue("id", id),
            map: MapTeacher);
}

