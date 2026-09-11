using Npgsql;
using StudentConnect.Api.Models;

namespace StudentConnect.Api.Data.Stores;

public class AttendanceStore(Database db)
{
    private const string SelectWithNames = """
        SELECT a."Id", a."StudentId", a."SubjectId", a."Date", a."Status",
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
    };

    public async Task<List<Attendance>> GetAllAsync() =>
        await db.QueryAsync($"""{SelectWithNames} ORDER BY a."Date" DESC""", bind: null, map: Map);

    public async Task<List<Attendance>> GetByStudentAsync(int studentId) =>
        await db.QueryAsync(
            $"""{SelectWithNames} WHERE a."StudentId" = @studentId ORDER BY a."Date" DESC""",
            p => p.AddWithValue("studentId", studentId),
            Map);

    public async Task<List<Attendance>> GetByStudentAndSubjectAsync(int studentId, int subjectId) =>
        await db.QueryAsync(
            $"""{SelectWithNames} WHERE a."StudentId" = @studentId AND a."SubjectId" = @subjectId ORDER BY a."Date" DESC""",
            p =>
            {
                p.AddWithValue("studentId", studentId);
                p.AddWithValue("subjectId", subjectId);
            },
            Map);

    public async Task<List<AttendanceSummaryDto>> GetSummaryAsync(int studentId) =>
        await db.QueryAsync(
            """
            SELECT ss."SubjectId", sub."Name" AS "SubjectName",
                   COUNT(a."Id") AS "TotalClasses",
                   COUNT(a."Id") FILTER (WHERE a."Status" = 'Present') AS "Present"
            FROM "StudentSubjects" ss
            JOIN "Subjects" sub ON sub."Id" = ss."SubjectId"
            LEFT JOIN "Attendance" a ON a."StudentId" = ss."StudentId" AND a."SubjectId" = ss."SubjectId"
            WHERE ss."StudentId" = @studentId
            GROUP BY ss."SubjectId", sub."Name"
            ORDER BY ss."SubjectId"
            """,
            p => p.AddWithValue("studentId", studentId),
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

    public async Task UpdateStatusAsync(int id, AttendanceStatus status) =>
        await db.ExecuteAsync(
            """UPDATE "Attendance" SET "Status" = @status WHERE "Id" = @id""",
            p =>
            {
                p.AddWithValue("id", id);
                p.AddWithValue("status", status.ToString());
            });

    public async Task DeleteAsync(int id) =>
        await db.ExecuteAsync("""DELETE FROM "Attendance" WHERE "Id" = @id""", p => p.AddWithValue("id", id));

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
