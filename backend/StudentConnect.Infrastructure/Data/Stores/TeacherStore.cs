using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;

namespace StudentConnect.Infrastructure.Data.Stores;

public class TeacherStore(Database db)
{
    private const string SelectWithUsername = """
        SELECT t."Id", t."TeacherId", t."FirstName", t."LastName", t."Email", t."SchoolId", u."Username"
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
            SchoolId = reader.GetNullableInt32("SchoolId") ?? 0,
        },
        reader.GetNullableString("Username"));

    public async Task<List<TeacherResponseDto>> GetAllAsync() =>
        await db.QueryAsync($"""{SelectWithUsername} ORDER BY t."Id" """, bind: null, map: Map);

    public async Task<TeacherResponseDto?> GetByIdAsync(int id, int schoolId) =>
        await db.QuerySingleOrDefaultAsync(
            $"""{SelectWithUsername} WHERE t."Id" = @id AND t."SchoolId" = @schoolId""",
            p =>
            {
                p.AddWithValue("id", id);
                p.AddWithValue("schoolId", schoolId);
            },
            map: Map);

    // Tenant-scoped — used by the multi-tenancy-aware TeachersController.
    public async Task<List<TeacherResponseDto>> GetAllAsync(int schoolId) =>
        await db.QueryAsync(
            $"""{SelectWithUsername} WHERE t."SchoolId" = @schoolId ORDER BY t."Id" """,
            p => p.AddWithValue("schoolId", schoolId),
            map: Map);

    // Page/pageSize-bounded variant backing GET /api/teachers (see TeachersController).
    public async Task<(List<TeacherResponseDto> Items, int TotalCount)> GetPagedAsync(int schoolId, int page, int pageSize)
    {
        var items = await db.QueryAsync(
            $"""{SelectWithUsername} WHERE t."SchoolId" = @schoolId ORDER BY t."Id" LIMIT @pageSize OFFSET @offset""",
            p =>
            {
                p.AddWithValue("schoolId", schoolId);
                p.AddWithValue("pageSize", pageSize);
                p.AddWithValue("offset", (page - 1) * pageSize);
            },
            map: Map);

        var totalCount = await db.ExecuteScalarAsync<long>(
            """SELECT COUNT(*) FROM "Teachers" WHERE "SchoolId" = @schoolId""",
            p => p.AddWithValue("schoolId", schoolId));

        return (items, (int)totalCount);
    }

    public async Task<bool> ExistsAsync(int id) =>
        await db.ExistsAsync("""SELECT 1 FROM "Teachers" WHERE "Id" = @id""", p => p.AddWithValue("id", id));

    public async Task<bool> ExistsAsync(int id, int schoolId) =>
        await db.ExistsAsync(
            """SELECT 1 FROM "Teachers" WHERE "Id" = @id AND "SchoolId" = @schoolId""",
            p =>
            {
                p.AddWithValue("id", id);
                p.AddWithValue("schoolId", schoolId);
            });

    public async Task<bool> TeacherCodeExistsAsync(string teacherCode) =>
        await db.ExistsAsync("""SELECT 1 FROM "Teachers" WHERE "TeacherId" = @code""", p => p.AddWithValue("code", teacherCode));

    public async Task<bool> TeacherCodeExistsAsync(string teacherCode, int schoolId) =>
        await db.ExistsAsync(
            """SELECT 1 FROM "Teachers" WHERE "TeacherId" = @code AND "SchoolId" = @schoolId""",
            p =>
            {
                p.AddWithValue("code", teacherCode);
                p.AddWithValue("schoolId", schoolId);
            });

    public async Task<int> CreateAsync(Teacher teacher) =>
        await db.ExecuteScalarAsync<int>(
            """
            INSERT INTO "Teachers" ("TeacherId", "FirstName", "LastName", "Email", "SchoolId")
            VALUES (@teacherId, @firstName, @lastName, @email, @schoolId)
            RETURNING "Id"
            """,
            p =>
            {
                p.AddWithValue("teacherId", teacher.TeacherId);
                p.AddWithValue("firstName", teacher.FirstName);
                p.AddWithValue("lastName", teacher.LastName);
                p.AddWithValue("email", teacher.Email);
                p.AddWithValue("schoolId", teacher.SchoolId);
            });

    public async Task UpdateAsync(Teacher teacher) =>
        await db.ExecuteAsync(
            """UPDATE "Teachers" SET "FirstName" = @firstName, "LastName" = @lastName, "Email" = @email WHERE "Id" = @id""",
            p =>
            {
                p.AddWithValue("id", teacher.Id);
                p.AddWithValue("firstName", teacher.FirstName);
                p.AddWithValue("lastName", teacher.LastName);
                p.AddWithValue("email", teacher.Email);
            });

    public async Task DeleteAsync(int id) =>
        await db.ExecuteAsync("""DELETE FROM "Teachers" WHERE "Id" = @id""", p => p.AddWithValue("id", id));
}
