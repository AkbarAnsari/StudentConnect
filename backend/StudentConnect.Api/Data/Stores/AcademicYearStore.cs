using Npgsql;
using StudentConnect.Api.Models;

namespace StudentConnect.Api.Data.Stores;

public class AcademicYearStore(Database db)
{
    private static AcademicYear Map(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32("Id"),
        Name = reader.GetString("Name"),
        StartDate = reader.GetDateOnly("StartDate"),
        EndDate = reader.GetDateOnly("EndDate"),
        IsCurrent = reader.GetBoolean("IsCurrent"),
    };

    private const string Select = """SELECT "Id", "Name", "StartDate", "EndDate", "IsCurrent" FROM "AcademicYears" """;

    public async Task<List<AcademicYear>> GetAllAsync() =>
        await db.QueryAsync($"""{Select} ORDER BY "StartDate" DESC""", bind: null, map: Map);

    public async Task<AcademicYear?> GetByIdAsync(int id) =>
        await db.QuerySingleOrDefaultAsync($"""{Select} WHERE "Id" = @id""", p => p.AddWithValue("id", id), Map);

    public async Task<bool> ExistsAsync(int id) =>
        await db.ExistsAsync("""SELECT 1 FROM "AcademicYears" WHERE "Id" = @id""", p => p.AddWithValue("id", id));

    public async Task<int> CreateAsync(AcademicYear year)
    {
        if (year.IsCurrent)
        {
            await ClearCurrentFlagAsync();
        }

        return await db.ExecuteScalarAsync<int>(
            """
            INSERT INTO "AcademicYears" ("Name", "StartDate", "EndDate", "IsCurrent")
            VALUES (@name, @startDate, @endDate, @isCurrent)
            RETURNING "Id"
            """,
            p =>
            {
                p.AddWithValue("name", year.Name);
                p.AddWithValue("startDate", year.StartDate);
                p.AddWithValue("endDate", year.EndDate);
                p.AddWithValue("isCurrent", year.IsCurrent);
            });
    }

    // Only one academic year is ever "current" — setting a new one clears the flag on all others.
    private async Task ClearCurrentFlagAsync() =>
        await db.ExecuteAsync("""UPDATE "AcademicYears" SET "IsCurrent" = false WHERE "IsCurrent" = true""", bind: null);

    public async Task DeleteAsync(int id) =>
        await db.ExecuteAsync("""DELETE FROM "AcademicYears" WHERE "Id" = @id""", p => p.AddWithValue("id", id));
}
