using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;

namespace StudentConnect.Infrastructure.Data.Stores;

// Aggregate/reporting queries — deliberately computed in SQL (GROUP BY, FILTER, AVG) rather
// than pulled into memory and aggregated in C#, since that's what a database is for.
public class AnalyticsStore(Database db)
{
    public async Task<AnalyticsOverviewDto> GetOverviewAsync(int schoolId)
    {
        const string sql = """
            SELECT
                (SELECT COUNT(*) FROM "Students" WHERE "SchoolId" = @schoolId) AS "TotalStudents",
                (SELECT COUNT(*) FROM "Teachers" WHERE "SchoolId" = @schoolId) AS "TotalTeachers",
                (SELECT COUNT(*) FROM "Subjects" WHERE "SchoolId" = @schoolId) AS "TotalSubjects",
                (SELECT COUNT(*) FROM "Attendance" a JOIN "Students" s ON s."Id" = a."StudentId" WHERE s."SchoolId" = @schoolId) AS "AttendanceTotal",
                (SELECT COUNT(*) FROM "Attendance" a JOIN "Students" s ON s."Id" = a."StudentId" WHERE s."SchoolId" = @schoolId AND a."Status" = 'Present') AS "AttendancePresent",
                (SELECT COUNT(*) FROM "Marks" m JOIN "Students" s ON s."Id" = m."StudentId" WHERE s."SchoolId" = @schoolId) AS "MarksCount",
                (SELECT COALESCE(SUM(m."MarksObtained" * 100.0 / a."MaximumMarks"), 0)::double precision
                   FROM "Marks" m JOIN "Assessments" a ON a."Id" = m."AssessmentId" JOIN "Students" s ON s."Id" = m."StudentId"
                   WHERE s."SchoolId" = @schoolId) AS "MarksPercentageSum"
            """;

        var row = await db.QuerySingleOrDefaultAsync(
            sql,
            bind: p => p.AddWithValue("schoolId", schoolId),
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

    public async Task<List<GradeAttendanceDto>> GetAttendanceByGradeAsync(int schoolId) =>
        await db.QueryAsync(
            """
            SELECT s."GradeId", g."Name" AS "GradeName", COUNT(*) AS "Total", COUNT(*) FILTER (WHERE a."Status" = 'Present') AS "Present"
            FROM "Attendance" a
            JOIN "Students" s ON s."Id" = a."StudentId"
            JOIN "Grades" g ON g."Id" = s."GradeId"
            WHERE s."SchoolId" = @schoolId
            GROUP BY s."GradeId", g."Name"
            ORDER BY g."Name"
            """,
            bind: p => p.AddWithValue("schoolId", schoolId),
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

    public async Task<List<GradeMarksDto>> GetMarksByGradeAsync(int schoolId)
    {
        var rows = await db.QueryAsync(
            """
            SELECT s."GradeId", g."Name" AS "GradeName", AVG(m."MarksObtained" * 100.0 / a."MaximumMarks")::double precision AS "AveragePercentage"
            FROM "Marks" m
            JOIN "Students" s ON s."Id" = m."StudentId"
            JOIN "Assessments" a ON a."Id" = m."AssessmentId"
            JOIN "Grades" g ON g."Id" = s."GradeId"
            WHERE s."SchoolId" = @schoolId
            GROUP BY s."GradeId", g."Name"
            ORDER BY g."Name"
            """,
            bind: p => p.AddWithValue("schoolId", schoolId),
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

    public async Task<List<AtRiskStudentDto>> GetAtRiskStudentsAsync(int schoolId, double attendanceThreshold, double marksThreshold) =>
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
            WHERE s."SchoolId" = @schoolId
              AND ((att.total > 0 AND (att.present * 100.0 / att.total) < @attendanceThreshold)
               OR (ms.avg_pct IS NOT NULL AND ms.avg_pct < @marksThreshold))
            ORDER BY COALESCE(att.present * 100.0 / NULLIF(att.total, 0), 100)
            """,
            p =>
            {
                p.AddWithValue("schoolId", schoolId);
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

    // Monthly attendance/marks rollup for the last N months, scoped to one school — the trend
    // chart's whole point is seeing change over time, so (unlike the other queries above) this
    // one is deliberately tenant-scoped via the Students join.
    public async Task<List<MonthlyTrendDto>> GetTrendAsync(int schoolId, int months)
    {
        var sinceDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-months));

        return await db.QueryAsync(
            """
            WITH attendance_monthly AS (
                SELECT date_trunc('month', a."Date"::timestamp) AS month,
                       COUNT(*) AS total, COUNT(*) FILTER (WHERE a."Status" = 'Present') AS present
                FROM "Attendance" a
                JOIN "Students" st ON st."Id" = a."StudentId"
                WHERE st."SchoolId" = @schoolId AND a."Date" >= @sinceDate
                GROUP BY month
            ),
            marks_monthly AS (
                SELECT date_trunc('month', asm."Date"::timestamp) AS month,
                       SUM(m."MarksObtained") AS obtained, SUM(asm."MaximumMarks") AS maximum
                FROM "Marks" m
                JOIN "Assessments" asm ON asm."Id" = m."AssessmentId"
                JOIN "Students" st ON st."Id" = m."StudentId"
                WHERE st."SchoolId" = @schoolId AND asm."Date" >= @sinceDate
                GROUP BY month
            )
            SELECT COALESCE(am.month, mm.month) AS "Month",
                   COALESCE(am.total, 0) AS "Total", COALESCE(am.present, 0) AS "Present",
                   COALESCE(mm.obtained, 0) AS "Obtained", COALESCE(mm.maximum, 0) AS "Maximum"
            FROM attendance_monthly am
            FULL OUTER JOIN marks_monthly mm ON am.month = mm.month
            ORDER BY "Month"
            """,
            p =>
            {
                p.AddWithValue("schoolId", schoolId);
                p.AddWithValue("sinceDate", sinceDate);
            },
            reader =>
            {
                var total = reader.GetInt64("Total");
                var present = reader.GetInt64("Present");
                var obtained = reader.GetInt64("Obtained");
                var maximum = reader.GetInt64("Maximum");
                return new MonthlyTrendDto
                {
                    Month = reader.GetFieldValue<DateTime>(reader.GetOrdinal("Month")).ToString("yyyy-MM"),
                    AttendancePercentage = total == 0 ? 0 : Math.Round(present * 100.0 / total, 1),
                    MarksPercentage = maximum == 0 ? 0 : Math.Round(obtained * 100.0 / maximum, 1),
                };
            });
    }
}
