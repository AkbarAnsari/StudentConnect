using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;

namespace StudentConnect.Infrastructure.Data.Stores;

// Aggregate/reporting queries — deliberately computed in SQL (GROUP BY, FILTER, AVG) rather
// than pulled into memory and aggregated in C#, since that's what a database is for.
public class AnalyticsStore(Database db)
{
    public async Task<AnalyticsOverviewDto> GetOverviewAsync()
    {
        const string sql = """
            SELECT
                (SELECT COUNT(*) FROM "Students") AS "TotalStudents",
                (SELECT COUNT(*) FROM "Teachers") AS "TotalTeachers",
                (SELECT COUNT(*) FROM "Subjects") AS "TotalSubjects",
                (SELECT COUNT(*) FROM "Attendance") AS "AttendanceTotal",
                (SELECT COUNT(*) FROM "Attendance" WHERE "Status" = 'Present') AS "AttendancePresent",
                (SELECT COUNT(*) FROM "Marks") AS "MarksCount",
                (SELECT COALESCE(SUM(m."MarksObtained" * 100.0 / a."MaximumMarks"), 0)::double precision
                   FROM "Marks" m JOIN "Assessments" a ON a."Id" = m."AssessmentId") AS "MarksPercentageSum"
            """;

        var row = await db.QuerySingleOrDefaultAsync(
            sql,
            bind: null,
            map: reader => new
            {
                TotalStudents = reader.GetInt32("TotalStudents"),
                TotalTeachers = reader.GetInt32("TotalTeachers"),
                TotalSubjects = reader.GetInt32("TotalSubjects"),
                AttendanceTotal = reader.GetInt64("AttendanceTotal"),
                AttendancePresent = reader.GetInt64("AttendancePresent"),
                MarksCount = reader.GetInt64("MarksCount"),
                MarksPercentageSum = reader.GetDouble("MarksPercentageSum"),
            });

        return new AnalyticsOverviewDto
        {
            TotalStudents = row!.TotalStudents,
            TotalTeachers = row.TotalTeachers,
            TotalSubjects = row.TotalSubjects,
            OverallAttendancePercentage = row.AttendanceTotal > 0 ? Math.Round(row.AttendancePresent * 100.0 / row.AttendanceTotal, 1) : 0,
            AverageMarksPercentage = row.MarksCount > 0 ? Math.Round(row.MarksPercentageSum / row.MarksCount, 1) : 0,
        };
    }

    public async Task<List<GradeAttendanceDto>> GetAttendanceByGradeAsync() =>
        await db.QueryAsync(
            """
            SELECT s."GradeId", g."Name" AS "GradeName", COUNT(*) AS "Total", COUNT(*) FILTER (WHERE a."Status" = 'Present') AS "Present"
            FROM "Attendance" a
            JOIN "Students" s ON s."Id" = a."StudentId"
            JOIN "Grades" g ON g."Id" = s."GradeId"
            GROUP BY s."GradeId", g."Name"
            ORDER BY g."Name"
            """,
            bind: null,
            map: reader =>
            {
                var total = reader.GetInt64("Total");
                var present = reader.GetInt64("Present");
                return new GradeAttendanceDto
                {
                    GradeId = reader.GetInt32("GradeId"),
                    GradeName = reader.GetString("GradeName"),
                    TotalRecords = (int)total,
                    AttendancePercentage = total > 0 ? Math.Round(present * 100.0 / total, 1) : 0,
                };
            });

    public async Task<List<GradeMarksDto>> GetMarksByGradeAsync()
    {
        var rows = await db.QueryAsync(
            """
            SELECT s."GradeId", g."Name" AS "GradeName", AVG(m."MarksObtained" * 100.0 / a."MaximumMarks")::double precision AS "AveragePercentage"
            FROM "Marks" m
            JOIN "Students" s ON s."Id" = m."StudentId"
            JOIN "Assessments" a ON a."Id" = m."AssessmentId"
            JOIN "Grades" g ON g."Id" = s."GradeId"
            GROUP BY s."GradeId", g."Name"
            ORDER BY g."Name"
            """,
            bind: null,
            map: reader => new GradeMarksDto
            {
                GradeId = reader.GetInt32("GradeId"),
                GradeName = reader.GetString("GradeName"),
                AverageMarksPercentage = Math.Round(reader.GetDouble("AveragePercentage"), 1),
            });

        foreach (var row in rows)
        {
            row.ResultCategory = ResultCategory.From(row.AverageMarksPercentage);
        }

        return rows;
    }

    public async Task<List<AtRiskStudentDto>> GetAtRiskStudentsAsync(double attendanceThreshold, double marksThreshold) =>
        await db.QueryAsync(
            """
            WITH attendance_stats AS (
                SELECT "StudentId", COUNT(*) AS total, COUNT(*) FILTER (WHERE "Status" = 'Present') AS present
                FROM "Attendance"
                GROUP BY "StudentId"
            ),
            marks_stats AS (
                SELECT m."StudentId", AVG(m."MarksObtained" * 100.0 / a."MaximumMarks") AS avg_pct
                FROM "Marks" m
                JOIN "Assessments" a ON a."Id" = m."AssessmentId"
                GROUP BY m."StudentId"
            )
            SELECT
                s."Id", s."StudentId" AS "StudentCode", s."FirstName", s."LastName", g."Name" AS "GradeName",
                CASE WHEN att.total > 0 THEN ROUND((att.present * 100.0 / att.total)::numeric, 1)::double precision ELSE NULL END AS "AttendancePercentage",
                CASE WHEN ms.avg_pct IS NOT NULL THEN ROUND(ms.avg_pct::numeric, 1)::double precision ELSE NULL END AS "MarksPercentage"
            FROM "Students" s
            JOIN "Grades" g ON g."Id" = s."GradeId"
            LEFT JOIN attendance_stats att ON att."StudentId" = s."Id"
            LEFT JOIN marks_stats ms ON ms."StudentId" = s."Id"
            WHERE (att.total > 0 AND (att.present * 100.0 / att.total) < @attendanceThreshold)
               OR (ms.avg_pct IS NOT NULL AND ms.avg_pct < @marksThreshold)
            ORDER BY COALESCE(att.present * 100.0 / NULLIF(att.total, 0), 100)
            """,
            p =>
            {
                p.AddWithValue("attendanceThreshold", attendanceThreshold);
                p.AddWithValue("marksThreshold", marksThreshold);
            },
            reader => new AtRiskStudentDto
            {
                Id = reader.GetInt32("Id"),
                StudentId = reader.GetString("StudentCode"),
                FullName = $"{reader.GetString("FirstName")} {reader.GetString("LastName")}",
                GradeName = reader.GetString("GradeName"),
                AttendancePercentage = reader.IsDBNull(reader.GetOrdinal("AttendancePercentage")) ? null : reader.GetDouble("AttendancePercentage"),
                AverageMarksPercentage = reader.IsDBNull(reader.GetOrdinal("MarksPercentage")) ? null : reader.GetDouble("MarksPercentage"),
            });
}
