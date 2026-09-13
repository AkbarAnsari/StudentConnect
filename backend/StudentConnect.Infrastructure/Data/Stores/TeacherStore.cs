using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;

namespace StudentConnect.Infrastructure.Data.Stores;

public class TeacherStore(Database db)
{
    private const string SelectWithUsername = """
        SELECT t."Id", t."TeacherId", t."FirstName", t."LastName", t."Email", u."Username"
        FROM "Teachers" t
        LEFT JOIN "Users" u ON u."TeacherId" = t."Id"
        """;

    private static TeacherResponseDto Map(NpgsqlDataReader reader) => TeacherResponseDto.FromEntity(
        new Teacher
        {
            Id = reader.GetInt32("Id"),
            TeacherId = reader.GetString("TeacherId"),
            FirstName = reader.GetString("FirstName"),
            LastName = reader.GetString("LastName"),
            Email = reader.GetString("Email"),
        },
        reader.GetNullableString("Username"));

    public async Task<List<TeacherResponseDto>> GetAllAsync() =>
        await db.QueryAsync($"""{SelectWithUsername} ORDER BY t."Id" """, bind: null, map: Map);

    public async Task<bool> ExistsAsync(int id) =>
        await db.ExistsAsync("""SELECT 1 FROM "Teachers" WHERE "Id" = @id""", p => p.AddWithValue("id", id));

    public async Task<bool> TeacherCodeExistsAsync(string teacherCode) =>
        await db.ExistsAsync("""SELECT 1 FROM "Teachers" WHERE "TeacherId" = @code""", p => p.AddWithValue("code", teacherCode));

    public async Task<int> CreateAsync(Teacher teacher) =>
        await db.ExecuteScalarAsync<int>(
            """
            INSERT INTO "Teachers" ("TeacherId", "FirstName", "LastName", "Email")
            VALUES (@teacherId, @firstName, @lastName, @email)
            RETURNING "Id"
            """,
            p =>
            {
                p.AddWithValue("teacherId", teacher.TeacherId);
                p.AddWithValue("firstName", teacher.FirstName);
                p.AddWithValue("lastName", teacher.LastName);
                p.AddWithValue("email", teacher.Email);
            });

    public async Task DeleteAsync(int id) =>
        await db.ExecuteAsync("""DELETE FROM "Teachers" WHERE "Id" = @id""", p => p.AddWithValue("id", id));
}
