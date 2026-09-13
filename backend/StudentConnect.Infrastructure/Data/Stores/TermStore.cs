using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;

namespace StudentConnect.Infrastructure.Data.Stores;

public class TermStore(Database db)
{
    private const string SelectWithYear = """
        SELECT t."Id", t."AcademicYearId", t."Name", t."StartDate", t."EndDate", ay."Name" AS "AcademicYearName"
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
    };

    public async Task<List<Term>> GetAllAsync() =>
        await db.QueryAsync($"""{SelectWithYear} ORDER BY t."StartDate" DESC""", bind: null, map: Map);

    public async Task<List<Term>> GetByAcademicYearAsync(int academicYearId) =>
        await db.QueryAsync(
            $"""{SelectWithYear} WHERE t."AcademicYearId" = @academicYearId ORDER BY t."StartDate" """,
            p => p.AddWithValue("academicYearId", academicYearId),
            Map);

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
}
