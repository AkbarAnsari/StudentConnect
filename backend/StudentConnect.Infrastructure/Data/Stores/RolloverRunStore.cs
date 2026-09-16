using Npgsql;
using NpgsqlTypes;
using StudentConnect.Application;

namespace StudentConnect.Infrastructure.Data.Stores;

public class RolloverRunStore(Database db)
{
    private const string Select = """
        SELECT r."Id", r."AcademicYearId", ay."Name" AS "AcademicYearName", r."ActorUserId",
               u."Username" AS "ActorName", r."CreatedAtUtc", r."IsForced", r."PromotedCount",
               r."SkippedCount", r."Snapshot"
        FROM "RolloverRuns" r
        JOIN "AcademicYears" ay ON ay."Id" = r."AcademicYearId"
        JOIN "Users" u ON u."Id" = r."ActorUserId"
        """;

    private static RolloverRunDto Map(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32("Id"),
        AcademicYearId = reader.GetInt32("AcademicYearId"),
        AcademicYearName = reader.GetString("AcademicYearName"),
        ActorUserId = reader.GetInt32("ActorUserId"),
        ActorName = reader.GetString("ActorName"),
        CreatedAtUtc = reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc")).ToUniversalTime(),
        IsForced = reader.GetBoolean("IsForced"),
        PromotedCount = reader.GetInt32("PromotedCount"),
        SkippedCount = reader.GetInt32("SkippedCount"),
    };

    public Task<List<RolloverRunDto>> GetAllAsync(int schoolId) =>
        db.QueryAsync(
            $"""{Select} WHERE r."SchoolId" = @schoolId ORDER BY r."CreatedAtUtc" DESC""",
            p => p.AddWithValue("schoolId", schoolId),
            Map);

    public Task<RolloverRunDto?> GetByIdAsync(int id, int schoolId) =>
        db.QuerySingleOrDefaultAsync(
            $"""{Select} WHERE r."Id" = @id AND r."SchoolId" = @schoolId""",
            p => { p.AddWithValue("id", id); p.AddWithValue("schoolId", schoolId); },
            Map);

    public Task<string?> GetSnapshotAsync(int id, int schoolId) =>
        db.ExecuteScalarAsync<string>(
            """SELECT "Snapshot"::text FROM "RolloverRuns" WHERE "Id" = @id AND "SchoolId" = @schoolId""",
            p => { p.AddWithValue("id", id); p.AddWithValue("schoolId", schoolId); });

    public static async Task<int> InsertInTxAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int schoolId,
        int academicYearId,
        int actorUserId,
        bool isForced,
        int promotedCount,
        int skippedCount,
        string snapshot)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO "RolloverRuns" ("SchoolId", "AcademicYearId", "ActorUserId", "IsForced", "PromotedCount", "SkippedCount", "Snapshot")
            VALUES (@schoolId, @academicYearId, @actorUserId, @isForced, @promotedCount, @skippedCount, @snapshot)
            RETURNING "Id"
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("schoolId", schoolId);
        command.Parameters.AddWithValue("academicYearId", academicYearId);
        command.Parameters.AddWithValue("actorUserId", actorUserId);
        command.Parameters.AddWithValue("isForced", isForced);
        command.Parameters.AddWithValue("promotedCount", promotedCount);
        command.Parameters.AddWithValue("skippedCount", skippedCount);
        command.Parameters.AddWithValue("snapshot", NpgsqlDbType.Jsonb, snapshot);
        return (int)(await command.ExecuteScalarAsync())!;
    }
}