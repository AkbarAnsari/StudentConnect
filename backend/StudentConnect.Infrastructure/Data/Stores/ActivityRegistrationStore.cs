using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;

namespace StudentConnect.Infrastructure.Data.Stores;

public class ActivityRegistrationStore(Database db)
{
    private const string SelectWithNames = """
        SELECT r."Id", r."StudentId", r."ActivityId", r."RegistrationDate", r."Status",
               st."FirstName" AS "StudentFirstName", st."LastName" AS "StudentLastName",
               act."Title" AS "ActivityTitle", act."Date" AS "ActivityDate", act."Location" AS "ActivityLocation"
        FROM "ActivityRegistrations" r
        JOIN "Students" st ON st."Id" = r."StudentId"
        JOIN "Activities" act ON act."Id" = r."ActivityId"
        """;

    private static ActivityRegistration Map(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32("Id"),
        StudentId = reader.GetInt32("StudentId"),
        Student = new Student { FirstName = reader.GetString("StudentFirstName"), LastName = reader.GetString("StudentLastName") },
        ActivityId = reader.GetInt32("ActivityId"),
        Activity = new Activity
        {
            Title = reader.GetString("ActivityTitle"),
            Date = reader.GetDateTimeUtc("ActivityDate"),
            Location = reader.GetString("ActivityLocation"),
        },
        RegistrationDate = reader.GetDateTimeUtc("RegistrationDate"),
        Status = reader.GetEnum<ActivityRegistrationStatus>("Status"),
    };

    public async Task<List<ActivityRegistration>> GetByStudentAsync(int studentId) =>
        await db.QueryAsync($"""{SelectWithNames} WHERE r."StudentId" = @studentId ORDER BY r."RegistrationDate" DESC""", p => p.AddWithValue("studentId", studentId), Map);

    public async Task<List<ActivityRegistration>> GetByActivityAsync(int activityId) =>
        await db.QueryAsync($"""{SelectWithNames} WHERE r."ActivityId" = @activityId ORDER BY r."RegistrationDate" """, p => p.AddWithValue("activityId", activityId), Map);

    public async Task<ActivityRegistration?> GetByIdAsync(int id) =>
        await db.QuerySingleOrDefaultAsync($"""{SelectWithNames} WHERE r."Id" = @id""", p => p.AddWithValue("id", id), Map);

    public async Task<bool> ExistsAnyForActivityAsync(int activityId) =>
        await db.ExistsAsync("""SELECT 1 FROM "ActivityRegistrations" WHERE "ActivityId" = @activityId""", p => p.AddWithValue("activityId", activityId));

    public async Task<int> CountActiveAsync(int activityId)
    {
        var count = await db.ExecuteScalarAsync<long>(
            """SELECT COUNT(*) FROM "ActivityRegistrations" WHERE "ActivityId" = @activityId AND "Status" = 'Registered'""",
            p => p.AddWithValue("activityId", activityId));
        return (int)count;
    }

    public async Task<int> CountWaitlistedAsync(int activityId)
    {
        var count = await db.ExecuteScalarAsync<long>(
            """SELECT COUNT(*) FROM "ActivityRegistrations" WHERE "ActivityId" = @activityId AND "Status" = 'Waitlisted'""",
            p => p.AddWithValue("activityId", activityId));
        return (int)count;
    }

    public async Task UpdateStatusAsync(int id, ActivityRegistrationStatus status) =>
        await db.ExecuteAsync(
            """UPDATE "ActivityRegistrations" SET "Status" = @status WHERE "Id" = @id""",
            p =>
            {
                p.AddWithValue("id", id);
                p.AddWithValue("status", status.ToString());
            });

    // --- Transactional registration flow (mirrors the original EF BeginTransactionAsync usage) ---
    // Narrows (without fully eliminating) the race where two near-simultaneous requests both
    // read a capacity count that's about to become stale.
    //
    // Every method below re-checks tenant ownership (Activity -> Grade -> SchoolId, since
    // Activities have no direct SchoolId column) inside its own SQL, so these helpers are safe
    // to call even if a future caller forgets the controller-level tenant check.

    private const string ActivityBelongsToSchool = """EXISTS (SELECT 1 FROM "Activities" a JOIN "Grades" g ON g."Id" = a."GradeId" WHERE a."Id" = @activityId AND g."SchoolId" = @schoolId)""";
    private const string RegistrationBelongsToSchool = """EXISTS (SELECT 1 FROM "Activities" a JOIN "Grades" g ON g."Id" = a."GradeId" WHERE a."Id" = r."ActivityId" AND g."SchoolId" = @schoolId)""";

    public async Task<(int? Id, ActivityRegistrationStatus? Status)> GetExistingInTxAsync(NpgsqlConnection connection, NpgsqlTransaction tx, int studentId, int activityId, int schoolId)
    {
        await using var command = new NpgsqlCommand(
            $"""SELECT "Id", "Status" FROM "ActivityRegistrations" WHERE "StudentId" = @studentId AND "ActivityId" = @activityId AND {ActivityBelongsToSchool}""",
            connection, tx);
        command.Parameters.AddWithValue("studentId", studentId);
        command.Parameters.AddWithValue("activityId", activityId);
        command.Parameters.AddWithValue("schoolId", schoolId);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return (null, null);
        }
        return (reader.GetInt32("Id"), reader.GetEnum<ActivityRegistrationStatus>("Status"));
    }

    public async Task<int> CountActiveInTxAsync(NpgsqlConnection connection, NpgsqlTransaction tx, int activityId, int schoolId)
    {
        await using var command = new NpgsqlCommand(
            $"""SELECT COUNT(*) FROM "ActivityRegistrations" WHERE "ActivityId" = @activityId AND "Status" = 'Registered' AND {ActivityBelongsToSchool}""",
            connection, tx);
        command.Parameters.AddWithValue("activityId", activityId);
        command.Parameters.AddWithValue("schoolId", schoolId);
        return (int)(long)(await command.ExecuteScalarAsync())!;
    }

    // Returns null (instead of inserting) when the activity doesn't belong to schoolId.
    public async Task<int?> InsertInTxAsync(NpgsqlConnection connection, NpgsqlTransaction tx, int studentId, int activityId, DateTime registrationDate, ActivityRegistrationStatus status, int schoolId)
    {
        await using var command = new NpgsqlCommand(
            $"""
            INSERT INTO "ActivityRegistrations" ("StudentId", "ActivityId", "RegistrationDate", "Status")
            SELECT @studentId, @activityId, @registrationDate, @status
            WHERE {ActivityBelongsToSchool}
            RETURNING "Id"
            """,
            connection, tx);
        command.Parameters.AddWithValue("studentId", studentId);
        command.Parameters.AddWithValue("activityId", activityId);
        command.Parameters.AddWithValue("registrationDate", registrationDate);
        command.Parameters.AddWithValue("status", status.ToString());
        command.Parameters.AddWithValue("schoolId", schoolId);
        var result = await command.ExecuteScalarAsync();
        return result is null ? null : (int)result;
    }

    public async Task ReactivateInTxAsync(NpgsqlConnection connection, NpgsqlTransaction tx, int id, DateTime registrationDate, ActivityRegistrationStatus status, int schoolId)
    {
        await using var command = new NpgsqlCommand(
            $"""UPDATE "ActivityRegistrations" r SET "Status" = @status, "RegistrationDate" = @registrationDate WHERE r."Id" = @id AND {RegistrationBelongsToSchool}""",
            connection, tx);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("registrationDate", registrationDate);
        command.Parameters.AddWithValue("status", status.ToString());
        command.Parameters.AddWithValue("schoolId", schoolId);
        await command.ExecuteNonQueryAsync();
    }

    public async Task UpdateStatusInTxAsync(NpgsqlConnection connection, NpgsqlTransaction tx, int id, ActivityRegistrationStatus status, int schoolId)
    {
        await using var command = new NpgsqlCommand(
            $"""UPDATE "ActivityRegistrations" r SET "Status" = @status WHERE r."Id" = @id AND {RegistrationBelongsToSchool}""",
            connection, tx);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("status", status.ToString());
        command.Parameters.AddWithValue("schoolId", schoolId);
        await command.ExecuteNonQueryAsync();
    }

    // Earliest-waitlisted-first — the next student in line when a Registered seat frees up.
    public async Task<int?> GetEarliestWaitlistedInTxAsync(NpgsqlConnection connection, NpgsqlTransaction tx, int activityId, int schoolId)
    {
        await using var command = new NpgsqlCommand(
            $"""
            SELECT "Id" FROM "ActivityRegistrations"
            WHERE "ActivityId" = @activityId AND "Status" = 'Waitlisted' AND {ActivityBelongsToSchool}
            ORDER BY "RegistrationDate" LIMIT 1
            """,
            connection, tx);
        command.Parameters.AddWithValue("activityId", activityId);
        command.Parameters.AddWithValue("schoolId", schoolId);
        var result = await command.ExecuteScalarAsync();
        return result is null ? null : (int)result;
    }

    public async Task PromoteInTxAsync(NpgsqlConnection connection, NpgsqlTransaction tx, int id, int schoolId)
    {
        await using var command = new NpgsqlCommand(
            $"""UPDATE "ActivityRegistrations" r SET "Status" = 'Registered' WHERE r."Id" = @id AND {RegistrationBelongsToSchool}""",
            connection, tx);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("schoolId", schoolId);
        await command.ExecuteNonQueryAsync();
    }
}
