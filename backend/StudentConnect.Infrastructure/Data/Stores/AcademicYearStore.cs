using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;

namespace StudentConnect.Infrastructure.Data.Stores;

public class AcademicYearStore(Database db)
{
    private static AcademicYear Map(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32("Id"),
        Name = reader.GetString("Name"),
        StartDate = reader.GetDateOnly("StartDate"),
        EndDate = reader.GetDateOnly("EndDate"),
        IsCurrent = reader.GetBoolean("IsCurrent"),
        SchoolId = reader.GetNullableInt32("SchoolId") ?? 0,
        RolledOverAtUtc = reader.GetNullableDateTimeUtc("RolledOverAtUtc"),
        IsArchived = reader.GetBoolean("IsArchived"),
    };

    private const string Select = """SELECT "Id", "Name", "StartDate", "EndDate", "IsCurrent", "SchoolId", "RolledOverAtUtc", "IsArchived" FROM "AcademicYears" """;

    public async Task<List<AcademicYear>> GetAllAsync() =>
        await db.QueryAsync($"""{Select} ORDER BY "StartDate" DESC""", bind: null, map: Map);

    // Tenant-scoped — used by the multi-tenancy-aware AcademicYearsController. Archived years are
    // excluded by default since most callers (dropdowns, rollover) only care about active years.
    public async Task<List<AcademicYear>> GetAllAsync(int schoolId, bool includeArchived = false) =>
        await db.QueryAsync(
            includeArchived
                ? $"""{Select} WHERE "SchoolId" = @schoolId ORDER BY "StartDate" DESC"""
                : $"""{Select} WHERE "SchoolId" = @schoolId AND "IsArchived" = false ORDER BY "StartDate" DESC""",
            p => p.AddWithValue("schoolId", schoolId),
            Map);

    public async Task<AcademicYear?> GetByIdAsync(int id) =>
        await db.QuerySingleOrDefaultAsync($"""{Select} WHERE "Id" = @id""", p => p.AddWithValue("id", id), Map);

    public async Task<AcademicYear?> GetByIdAsync(int id, int schoolId) =>
        await db.QuerySingleOrDefaultAsync(
            $"""{Select} WHERE "Id" = @id AND "SchoolId" = @schoolId""",
            p =>
            {
                p.AddWithValue("id", id);
                p.AddWithValue("schoolId", schoolId);
            },
            Map);

    public async Task<bool> ExistsAsync(int id) =>
        await db.ExistsAsync("""SELECT 1 FROM "AcademicYears" WHERE "Id" = @id""", p => p.AddWithValue("id", id));

    public async Task<bool> ExistsAsync(int id, int schoolId) =>
        await db.ExistsAsync(
            """SELECT 1 FROM "AcademicYears" WHERE "Id" = @id AND "SchoolId" = @schoolId""",
            p =>
            {
                p.AddWithValue("id", id);
                p.AddWithValue("schoolId", schoolId);
            });

    public async Task<int> CreateAsync(AcademicYear year)
    {
        if (year.IsCurrent)
        {
            await ClearCurrentFlagAsync(year.SchoolId);
        }

        return await db.ExecuteScalarAsync<int>(
            """
            INSERT INTO "AcademicYears" ("Name", "StartDate", "EndDate", "IsCurrent", "SchoolId")
            VALUES (@name, @startDate, @endDate, @isCurrent, @schoolId)
            RETURNING "Id"
            """,
            p =>
            {
                p.AddWithValue("name", year.Name);
                p.AddWithValue("startDate", year.StartDate);
                p.AddWithValue("endDate", year.EndDate);
                p.AddWithValue("isCurrent", year.IsCurrent);
                p.AddWithValue("schoolId", year.SchoolId);
            });
    }

    // Only one academic year per SCHOOL is ever "current" — setting a new one clears the flag
    // on all other years belonging to the SAME school only (never touches other tenants' data).
    private async Task ClearCurrentFlagAsync(int schoolId) =>
        await db.ExecuteAsync(
            """UPDATE "AcademicYears" SET "IsCurrent" = false WHERE "IsCurrent" = true AND "SchoolId" = @schoolId""",
            p => p.AddWithValue("schoolId", schoolId));

    public async Task DeleteAsync(int id) =>
        await db.ExecuteAsync("""DELETE FROM "AcademicYears" WHERE "Id" = @id""", p => p.AddWithValue("id", id));

    // Soft-delete: keeps the row (and its Terms/rollover history) intact but hides it from
    // the active-years views used elsewhere in the app.
    public async Task ArchiveAsync(int id) =>
        await db.ExecuteAsync("""UPDATE "AcademicYears" SET "IsArchived" = true WHERE "Id" = @id""", p => p.AddWithValue("id", id));

    public async Task RestoreAsync(int id) =>
        await db.ExecuteAsync("""UPDATE "AcademicYears" SET "IsArchived" = false WHERE "Id" = @id""", p => p.AddWithValue("id", id));

    // Used inside the rollover-commit transaction (see AcademicYearsController) to guard
    // against accidentally re-running the same year's promotion.
    public async Task MarkRolledOverInTxAsync(NpgsqlConnection connection, NpgsqlTransaction tx, int academicYearId)
    {
        await using var command = new NpgsqlCommand(
            """UPDATE "AcademicYears" SET "RolledOverAtUtc" = now() WHERE "Id" = @id""",
            connection,
            tx);
        command.Parameters.AddWithValue("id", academicYearId);
        await command.ExecuteNonQueryAsync();
    }
}
