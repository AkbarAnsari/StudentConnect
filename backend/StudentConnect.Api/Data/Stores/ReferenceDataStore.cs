using Npgsql;
using StudentConnect.Api.Models;

namespace StudentConnect.Api.Data.Stores;

// Grades/Subjects/Teachers are simple lookup tables — no create/update/delete from the API
// today (they're seeded via database/seed.sql), so these stores are read-only.
public class ReferenceDataStore(Database db)
{
    private static Grade MapGrade(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32("Id"),
        Name = reader.GetString("Name"),
    };

    private static Subject MapSubject(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32("Id"),
        Name = reader.GetString("Name"),
        Code = reader.GetString("Code"),
    };

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
            """SELECT "Id", "Name" FROM "Grades" ORDER BY "Id" """,
            bind: null,
            map: MapGrade);

    public async Task<Grade?> GetGradeByIdAsync(int id) =>
        await db.QuerySingleOrDefaultAsync(
            """SELECT "Id", "Name" FROM "Grades" WHERE "Id" = @id""",
            bind: p => p.AddWithValue("id", id),
            map: MapGrade);

    public async Task<bool> GradeExistsAsync(int id) =>
        await db.ExistsAsync(
            """SELECT 1 FROM "Grades" WHERE "Id" = @id""",
            bind: p => p.AddWithValue("id", id));

    public async Task<List<Subject>> GetAllSubjectsAsync() =>
        await db.QueryAsync(
            """SELECT "Id", "Name", "Code" FROM "Subjects" ORDER BY "Id" """,
            bind: null,
            map: MapSubject);

    public async Task<Subject?> GetSubjectByIdAsync(int id) =>
        await db.QuerySingleOrDefaultAsync(
            """SELECT "Id", "Name", "Code" FROM "Subjects" WHERE "Id" = @id""",
            bind: p => p.AddWithValue("id", id),
            map: MapSubject);

    public async Task<bool> SubjectExistsAsync(int id) =>
        await db.ExistsAsync(
            """SELECT 1 FROM "Subjects" WHERE "Id" = @id""",
            bind: p => p.AddWithValue("id", id));

    public async Task<bool> TeacherExistsAsync(int id) =>
        await db.ExistsAsync(
            """SELECT 1 FROM "Teachers" WHERE "Id" = @id""",
            bind: p => p.AddWithValue("id", id));

    public async Task<Teacher?> GetTeacherByIdAsync(int id) =>
        await db.QuerySingleOrDefaultAsync(
            """SELECT "Id", "TeacherId", "FirstName", "LastName", "Email" FROM "Teachers" WHERE "Id" = @id""",
            bind: p => p.AddWithValue("id", id),
            map: MapTeacher);
}

