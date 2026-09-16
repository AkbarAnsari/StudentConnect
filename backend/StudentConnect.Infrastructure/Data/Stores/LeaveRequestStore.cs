using Npgsql;
using StudentConnect.Application;

namespace StudentConnect.Infrastructure.Data.Stores;

public class LeaveRequestStore(Database db)
{
    private const string Select = """
        SELECT lr."Id", lr."StudentId", concat_ws(' ', s."FirstName", s."LastName") AS "StudentName",
               lr."RequestedByUserId", ru."Username" AS "RequestedByName", lr."LeaveDate", lr."Reason", lr."Status",
               lr."DecisionReason", du."Username" AS "DecidedByName", lr."RequestedAtUtc", lr."DecidedAtUtc"
        FROM "LeaveRequests" lr
        JOIN "Students" s ON s."Id" = lr."StudentId"
        JOIN "Users" ru ON ru."Id" = lr."RequestedByUserId"
        LEFT JOIN "Users" du ON du."Id" = lr."DecidedByUserId"
        """;

    public async Task<List<LeaveRequestDto>> GetForSchoolAsync(int schoolId, string? status) =>
        await db.QueryAsync(
            string.IsNullOrWhiteSpace(status)
                ? $"""{Select} WHERE lr."SchoolId" = @schoolId ORDER BY lr."LeaveDate" DESC, lr."RequestedAtUtc" DESC"""
                : $"""{Select} WHERE lr."SchoolId" = @schoolId AND lr."Status" = @status ORDER BY lr."LeaveDate" DESC, lr."RequestedAtUtc" DESC""",
            p => { p.AddWithValue("schoolId", schoolId); if (!string.IsNullOrWhiteSpace(status)) p.AddWithValue("status", status); },
            Map);

    public async Task<List<LeaveRequestDto>> GetForUserAsync(int userId, int schoolId) =>
        await db.QueryAsync(
            $"""{Select} WHERE lr."SchoolId" = @schoolId AND (lr."RequestedByUserId" = @userId OR lr."StudentId" IN (SELECT "StudentId" FROM "ParentStudents" WHERE "ParentUserId" = @userId) OR lr."StudentId" IN (SELECT "StudentId" FROM "Users" WHERE "Id" = @userId)) ORDER BY lr."LeaveDate" DESC""",
            p => { p.AddWithValue("userId", userId); p.AddWithValue("schoolId", schoolId); },
            Map);

    public async Task<int> CreateAsync(int schoolId, int studentId, int requestedByUserId, DateOnly leaveDate, string reason) =>
        await db.ExecuteScalarAsync<int>(
            """INSERT INTO "LeaveRequests" ("SchoolId", "StudentId", "RequestedByUserId", "LeaveDate", "Reason") VALUES (@schoolId, @studentId, @requestedByUserId, @leaveDate, @reason) RETURNING "Id" """,
            p => { p.AddWithValue("schoolId", schoolId); p.AddWithValue("studentId", studentId); p.AddWithValue("requestedByUserId", requestedByUserId); p.AddWithValue("leaveDate", leaveDate); p.AddWithValue("reason", reason); });

    public async Task<LeaveRequestDto?> GetByIdAsync(int id, int schoolId) =>
        await db.QuerySingleOrDefaultAsync($"""{Select} WHERE lr."Id" = @id AND lr."SchoolId" = @schoolId""",
            p => { p.AddWithValue("id", id); p.AddWithValue("schoolId", schoolId); }, Map);

    public async Task<bool> DecideAsync(int id, int schoolId, string decision, string? reason, int decidedByUserId) =>
        await db.WithTransactionAsync(async (connection, transaction) =>
        {
            await using var update = new NpgsqlCommand(
                """UPDATE "LeaveRequests" SET "Status" = @decision, "DecisionReason" = @reason, "DecidedByUserId" = @decidedByUserId, "DecidedAtUtc" = now() WHERE "Id" = @id AND "SchoolId" = @schoolId AND "Status" = 'Pending' RETURNING "StudentId", "LeaveDate" """,
                connection, transaction);
            update.Parameters.AddWithValue("id", id); update.Parameters.AddWithValue("schoolId", schoolId);
            update.Parameters.AddWithValue("decision", decision); update.Parameters.AddWithValue("reason", (object?)reason ?? DBNull.Value);
            update.Parameters.AddWithValue("decidedByUserId", decidedByUserId);
            int studentId;
            DateOnly leaveDate;
            await using (var reader = await update.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync()) return false;
                studentId = reader.GetInt32(0);
                leaveDate = reader.GetFieldValue<DateOnly>(1);
            }

            if (decision == "Approved")
            {
                await using var corrections = new NpgsqlCommand(
                    """
                    INSERT INTO "AttendanceCorrections" ("AttendanceId", "ChangedByUserId", "PreviousStatus", "NewStatus", "Reason")
                    SELECT "Id", @decidedByUserId, "Status", 'Excused', @correctionReason FROM "Attendance"
                    WHERE "StudentId" = @studentId AND "Date" = @leaveDate AND "Status" = 'Absent';
                    UPDATE "Attendance" SET "Status" = 'Excused', "CorrectionReason" = @correctionReason
                    WHERE "StudentId" = @studentId AND "Date" = @leaveDate AND "Status" = 'Absent';
                    """,
                    connection, transaction);
                corrections.Parameters.AddWithValue("decidedByUserId", decidedByUserId);
                corrections.Parameters.AddWithValue("studentId", studentId);
                corrections.Parameters.AddWithValue("leaveDate", leaveDate);
                corrections.Parameters.AddWithValue("correctionReason", $"Approved leave request {id}");
                await corrections.ExecuteNonQueryAsync();
            }
            return true;
        });

    private static LeaveRequestDto Map(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32("Id"), StudentId = reader.GetInt32("StudentId"), StudentName = reader.GetString("StudentName"),
        RequestedByUserId = reader.GetInt32("RequestedByUserId"), RequestedByName = reader.GetString("RequestedByName"),
        LeaveDate = reader.GetDateOnly("LeaveDate"), Reason = reader.GetString("Reason"), Status = reader.GetString("Status"),
        DecisionReason = reader.GetNullableString("DecisionReason"), DecidedByName = reader.GetNullableString("DecidedByName"),
        RequestedAtUtc = reader.GetDateTimeUtc("RequestedAtUtc"), DecidedAtUtc = reader.GetNullableDateTimeUtc("DecidedAtUtc"),
    };
}