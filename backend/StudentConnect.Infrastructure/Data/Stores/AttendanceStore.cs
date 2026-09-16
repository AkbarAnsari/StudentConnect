using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;

namespace StudentConnect.Infrastructure.Data.Stores;

public class AttendanceStore(Database db)
{
    private const string SelectWithNames = """
        SELECT a."Id", a."StudentId", a."SubjectId", a."Date", a."Status", a."CorrectionReason",
               st."FirstName" AS "StudentFirstName", st."LastName" AS "StudentLastName",
               sub."Name" AS "SubjectName"
        FROM "Attendance" a
        JOIN "Students" st ON st."Id" = a."StudentId"
        JOIN "Subjects" sub ON sub."Id" = a."SubjectId"
        """;

    private static Attendance Map(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32("Id"),
        StudentId = reader.GetInt32("StudentId"),
        Student = new Student { FirstName = reader.GetString("StudentFirstName"), LastName = reader.GetString("StudentLastName") },
        SubjectId = reader.GetInt32("SubjectId"),
        Subject = new Subject { Name = reader.GetString("SubjectName") },
        Date = reader.GetDateOnly("Date"),
        Status = reader.GetEnum<AttendanceStatus>("Status"),
        CorrectionReason = reader.GetNullableString("CorrectionReason"),
    };

    public async Task<List<Attendance>> GetAllAsync(int schoolId) =>
        await db.QueryAsync(
            $"""{SelectWithNames} WHERE st."SchoolId" = @schoolId ORDER BY a."Date" DESC""",
            p => p.AddWithValue("schoolId", schoolId),
            Map);

    // Every already-recorded status for a subject+date, across all students — used to prefill
    // the teacher's bulk Record Attendance grid so re-saving doesn't silently overwrite it.
    public async Task<List<Attendance>> GetBySubjectAndDateAsync(int subjectId, DateOnly date) =>
        await db.QueryAsync(
            $"""{SelectWithNames} WHERE a."SubjectId" = @subjectId AND a."Date" = @date""",
            p =>
            {
                p.AddWithValue("subjectId", subjectId);
                p.AddWithValue("date", date);
            },
            Map);

    public async Task<List<Attendance>> GetByStudentAsync(int studentId) =>
        await db.QueryAsync(
            $"""{SelectWithNames} WHERE a."StudentId" = @studentId ORDER BY a."Date" DESC""",
            p => p.AddWithValue("studentId", studentId),
            Map);

    public async Task<List<Attendance>> GetByStudentAndSubjectAsync(int studentId, int subjectId, DateOnly? dateFrom = null, DateOnly? dateTo = null) =>
        await db.QueryAsync(
            $"""{SelectWithNames} WHERE a."StudentId" = @studentId AND a."SubjectId" = @subjectId AND (@dateFrom IS NULL OR a."Date" >= @dateFrom) AND (@dateTo IS NULL OR a."Date" <= @dateTo) ORDER BY a."Date" DESC""",
            p =>
            {
                p.AddWithValue("studentId", studentId);
                p.AddWithValue("subjectId", subjectId);
                p.Add("dateFrom", NpgsqlTypes.NpgsqlDbType.Date).Value = dateFrom is null ? DBNull.Value : dateFrom.Value;
                p.Add("dateTo", NpgsqlTypes.NpgsqlDbType.Date).Value = dateTo is null ? DBNull.Value : dateTo.Value;
            },
            Map);

    public async Task<List<AttendanceCorrectionDto>> GetCorrectionsForStudentSubjectAsync(
        int studentId,
        int subjectId,
        int schoolId,
        DateOnly? dateFrom = null,
        DateOnly? dateTo = null) =>
        await db.QueryAsync(
            """
            SELECT c."Id", c."AttendanceId", c."PreviousStatus", c."NewStatus", c."Reason", c."ChangedAtUtc",
                   u."Username" AS "ChangedByName"
            FROM "AttendanceCorrections" c
            JOIN "Attendance" a ON a."Id" = c."AttendanceId"
            JOIN "Students" st ON st."Id" = a."StudentId"
            JOIN "Users" u ON u."Id" = c."ChangedByUserId"
                        WHERE a."StudentId" = @studentId AND a."SubjectId" = @subjectId AND st."SchoolId" = @schoolId
                            AND (@dateFrom IS NULL OR a."Date" >= @dateFrom) AND (@dateTo IS NULL OR a."Date" <= @dateTo)
            ORDER BY c."ChangedAtUtc" DESC, c."Id" DESC
            """,
            p =>
            {
                p.AddWithValue("studentId", studentId);
                p.AddWithValue("subjectId", subjectId);
                p.AddWithValue("schoolId", schoolId);
                p.Add("dateFrom", NpgsqlTypes.NpgsqlDbType.Date).Value = dateFrom is null ? DBNull.Value : dateFrom.Value;
                p.Add("dateTo", NpgsqlTypes.NpgsqlDbType.Date).Value = dateTo is null ? DBNull.Value : dateTo.Value;
            },
            reader => new AttendanceCorrectionDto
            {
                Id = reader.GetInt32("Id"),
                AttendanceId = reader.GetInt32("AttendanceId"),
                PreviousStatus = reader.GetString("PreviousStatus"),
                NewStatus = reader.GetString("NewStatus"),
                Reason = reader.GetString("Reason"),
                ChangedAtUtc = reader.GetDateTimeUtc("ChangedAtUtc"),
                ChangedByName = reader.GetString("ChangedByName"),
            });

    public async Task<List<AttendanceSummaryDto>> GetSummaryAsync(int studentId, DateOnly? dateFrom = null, DateOnly? dateTo = null) =>
        await db.QueryAsync(
            """
            SELECT ss."SubjectId", sub."Name" AS "SubjectName",
                   COUNT(a."Id") FILTER (WHERE a."Status" <> 'Excused') AS "TotalClasses",
                   COUNT(a."Id") FILTER (WHERE a."Status" IN ('Present', 'Late')) AS "Present"
            FROM "StudentSubjects" ss
            JOIN "Subjects" sub ON sub."Id" = ss."SubjectId"
                        LEFT JOIN "Attendance" a ON a."StudentId" = ss."StudentId" AND a."SubjectId" = ss."SubjectId"
                            AND (@dateFrom IS NULL OR a."Date" >= @dateFrom) AND (@dateTo IS NULL OR a."Date" <= @dateTo)
            WHERE ss."StudentId" = @studentId
            GROUP BY ss."SubjectId", sub."Name"
            ORDER BY ss."SubjectId"
            """,
            p =>
            {
                p.AddWithValue("studentId", studentId);
                p.Add("dateFrom", NpgsqlTypes.NpgsqlDbType.Date).Value = dateFrom is null ? DBNull.Value : dateFrom.Value;
                p.Add("dateTo", NpgsqlTypes.NpgsqlDbType.Date).Value = dateTo is null ? DBNull.Value : dateTo.Value;
            },
            reader =>
            {
                var total = (int)reader.GetInt64("TotalClasses");
                var present = (int)reader.GetInt64("Present");
                return new AttendanceSummaryDto
                {
                    SubjectId = reader.GetInt32("SubjectId"),
                    SubjectName = reader.GetString("SubjectName"),
                    TotalClasses = total,
                    Present = present,
                    Absent = total - present,
                    Percentage = total == 0 ? 0 : Math.Round(present * 100.0 / total, 0),
                };
            });

    public async Task<bool> ExistsForStudentSubjectDateAsync(int studentId, int subjectId, DateOnly date) =>
        await db.ExistsAsync(
            """SELECT 1 FROM "Attendance" WHERE "StudentId" = @studentId AND "SubjectId" = @subjectId AND "Date" = @date""",
            p =>
            {
                p.AddWithValue("studentId", studentId);
                p.AddWithValue("subjectId", subjectId);
                p.AddWithValue("date", date);
            });

    public async Task<int> CreateAsync(Attendance attendance) =>
        await db.ExecuteScalarAsync<int>(
            """
            INSERT INTO "Attendance" ("StudentId", "SubjectId", "Date", "Status")
            VALUES (@studentId, @subjectId, @date, @status)
            RETURNING "Id"
            """,
            p =>
            {
                p.AddWithValue("studentId", attendance.StudentId);
                p.AddWithValue("subjectId", attendance.SubjectId);
                p.AddWithValue("date", attendance.Date);
                p.AddWithValue("status", attendance.Status.ToString());
            });

    public async Task<Attendance?> GetByIdAsync(int id) =>
        await db.QuerySingleOrDefaultAsync($"""{SelectWithNames} WHERE a."Id" = @id""", p => p.AddWithValue("id", id), Map);

    public async Task CorrectAsync(
        int id,
        AttendanceStatus previousStatus,
        AttendanceStatus status,
        string reason,
        int changedByUserId) =>
        await db.WithTransactionAsync(async (connection, tx) =>
        {
            await UpdateStatusInTxAsync(connection, tx, id, previousStatus, status, reason, changedByUserId);
            return true;
        });

    public async Task DeleteAsync(int id) =>
        await db.ExecuteAsync("""DELETE FROM "Attendance" WHERE "Id" = @id""", p => p.AddWithValue("id", id));

    // --- Transactional bulk-save support (mirrors ActivityRegistrationStore's *InTxAsync pattern) ---

    public async Task<int> CreateInTxAsync(NpgsqlConnection connection, NpgsqlTransaction tx, Attendance attendance)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO "Attendance" ("StudentId", "SubjectId", "Date", "Status")
            VALUES (@studentId, @subjectId, @date, @status)
            RETURNING "Id"
            """,
            connection, tx);
        command.Parameters.AddWithValue("studentId", attendance.StudentId);
        command.Parameters.AddWithValue("subjectId", attendance.SubjectId);
        command.Parameters.AddWithValue("date", attendance.Date);
        command.Parameters.AddWithValue("status", attendance.Status.ToString());
        return (int)(await command.ExecuteScalarAsync())!;
    }

    public async Task UpdateStatusInTxAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction tx,
        int id,
        AttendanceStatus previousStatus,
        AttendanceStatus status,
        string reason,
        int changedByUserId)
    {
        await using var command = new NpgsqlCommand(
            """UPDATE "Attendance" SET "Status" = @status, "CorrectionReason" = @reason WHERE "Id" = @id""",
            connection, tx);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("status", status.ToString());
        command.Parameters.AddWithValue("reason", reason);
        await command.ExecuteNonQueryAsync();

        await using var historyCommand = new NpgsqlCommand(
            """
            INSERT INTO "AttendanceCorrections" ("AttendanceId", "ChangedByUserId", "PreviousStatus", "NewStatus", "Reason")
            VALUES (@attendanceId, @changedByUserId, @previousStatus, @newStatus, @reason)
            """,
            connection,
            tx);
        historyCommand.Parameters.AddWithValue("attendanceId", id);
        historyCommand.Parameters.AddWithValue("changedByUserId", changedByUserId);
        historyCommand.Parameters.AddWithValue("previousStatus", previousStatus.ToString());
        historyCommand.Parameters.AddWithValue("newStatus", status.ToString());
        historyCommand.Parameters.AddWithValue("reason", reason);
        await historyCommand.ExecuteNonQueryAsync();
    }

    // Bulk save support: existing (studentId -> (id, status)) rows for a subject+date, so the
    // controller can decide update-in-place vs insert exactly like the original EF version.
    public async Task<Dictionary<int, (int Id, AttendanceStatus Status)>> GetExistingForSubjectDateAsync(int subjectId, DateOnly date, IReadOnlyCollection<int> studentIds)
    {
        if (studentIds.Count == 0)
        {
            return [];
        }

        var rows = await db.QueryAsync(
            """
            SELECT "Id", "StudentId", "Status" FROM "Attendance"
            WHERE "SubjectId" = @subjectId AND "Date" = @date AND "StudentId" = ANY(@studentIds)
            """,
            p =>
            {
                p.AddWithValue("subjectId", subjectId);
                p.AddWithValue("date", date);
                p.AddWithValue("studentIds", studentIds.ToArray());
            },
            reader => (StudentId: reader.GetInt32("StudentId"), Id: reader.GetInt32("Id"), Status: reader.GetEnum<AttendanceStatus>("Status")));

        return rows.ToDictionary(r => r.StudentId, r => (r.Id, r.Status));
    }
}
