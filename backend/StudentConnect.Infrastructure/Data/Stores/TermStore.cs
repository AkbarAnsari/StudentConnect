using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;

namespace StudentConnect.Infrastructure.Data.Stores;

public class TermStore(Database db)
{
    private const string SelectWithYear = """
        SELECT t."Id", t."AcademicYearId", t."Name", t."StartDate", t."EndDate", t."IsLocked", ay."Name" AS "AcademicYearName"
        FROM "Terms" t
        JOIN "AcademicYears" ay ON ay."Id" = t."AcademicYearId"
        """;

    private static Term Map(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32("Id"),
        AcademicYearId = reader.GetInt32("AcademicYearId"),
        AcademicYear = new AcademicYear { Name = reader.GetString("AcademicYearName") },
        Name = reader.GetString("Name"),
        StartDate = reader.GetDateOnly("StartDate"),
        EndDate = reader.GetDateOnly("EndDate"),
        IsLocked = reader.GetBoolean("IsLocked"),
    };

    public async Task<List<Term>> GetAllAsync() =>
        await db.QueryAsync($"""{SelectWithYear} ORDER BY t."StartDate" DESC""", bind: null, map: Map);

    // Tenant-scoped — Terms have no direct SchoolId column, so tenancy is resolved
    // transitively through the (NOT NULL) AcademicYearId foreign key.
    public async Task<List<Term>> GetAllAsync(int schoolId) =>
        await db.QueryAsync(
            $"""{SelectWithYear} WHERE ay."SchoolId" = @schoolId ORDER BY t."StartDate" DESC""",
            p => p.AddWithValue("schoolId", schoolId),
            Map);

    public async Task<List<Term>> GetByAcademicYearAsync(int academicYearId) =>
        await db.QueryAsync(
            $"""{SelectWithYear} WHERE t."AcademicYearId" = @academicYearId ORDER BY t."StartDate" """,
            p => p.AddWithValue("academicYearId", academicYearId),
            Map);

    // Tenant-scoped existence check used before deleting a Term by Id.
    public async Task<bool> ExistsAsync(int id, int schoolId) =>
        await db.ExistsAsync(
            """
            SELECT 1 FROM "Terms" t
            JOIN "AcademicYears" ay ON ay."Id" = t."AcademicYearId"
            WHERE t."Id" = @id AND ay."SchoolId" = @schoolId
            """,
            p =>
            {
                p.AddWithValue("id", id);
                p.AddWithValue("schoolId", schoolId);
            });

    public async Task<int> CreateAsync(Term term) =>
        await db.ExecuteScalarAsync<int>(
            """
            INSERT INTO "Terms" ("AcademicYearId", "Name", "StartDate", "EndDate")
            VALUES (@academicYearId, @name, @startDate, @endDate)
            RETURNING "Id"
            """,
            p =>
            {
                p.AddWithValue("academicYearId", term.AcademicYearId);
                p.AddWithValue("name", term.Name);
                p.AddWithValue("startDate", term.StartDate);
                p.AddWithValue("endDate", term.EndDate);
            });

    public async Task DeleteAsync(int id) =>
        await db.ExecuteAsync("""DELETE FROM "Terms" WHERE "Id" = @id""", p => p.AddWithValue("id", id));

    public async Task<bool> IsLockedAsync(int id) =>
        await db.ExecuteScalarAsync<bool>("""SELECT "IsLocked" FROM "Terms" WHERE "Id" = @id""", p => p.AddWithValue("id", id));

    public async Task LockAsync(int id) =>
        await db.ExecuteAsync("""UPDATE "Terms" SET "IsLocked" = true WHERE "Id" = @id""", p => p.AddWithValue("id", id));

    public async Task UnlockAsync(int id) =>
        await db.ExecuteAsync("""UPDATE "Terms" SET "IsLocked" = false WHERE "Id" = @id""", p => p.AddWithValue("id", id));
}
