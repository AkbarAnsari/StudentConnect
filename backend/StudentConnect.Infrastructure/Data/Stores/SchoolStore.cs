using Npgsql;
using StudentConnect.Domain;

namespace StudentConnect.Infrastructure.Data.Stores;

public class SchoolStore(Database db)
{
    private static School Map(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32("Id"),
        Name = reader.GetString("Name"),
        Code = reader.GetString("Code"),
        CreatedAtUtc = reader.GetDateTimeUtc("CreatedAtUtc"),
        IsActive = reader.GetBoolean("IsActive"),
    };

    public async Task<School?> GetByIdAsync(int id) =>
        await db.QuerySingleOrDefaultAsync(
            """SELECT "Id", "Name", "Code", "CreatedAtUtc", "IsActive" FROM "Schools" WHERE "Id" = @id""",
            p => p.AddWithValue("id", id),
            Map);

    public async Task<bool> CodeExistsAsync(string code) =>
        await db.ExistsAsync("""SELECT 1 FROM "Schools" WHERE "Code" = @code""", p => p.AddWithValue("code", code));

    public async Task<int> CreateAsync(string name, string code) =>
        await db.ExecuteScalarAsync<int>(
            """INSERT INTO "Schools" ("Name", "Code") VALUES (@name, @code) RETURNING "Id" """,
            p =>
            {
                p.AddWithValue("name", name);
                p.AddWithValue("code", code);
            });
}
