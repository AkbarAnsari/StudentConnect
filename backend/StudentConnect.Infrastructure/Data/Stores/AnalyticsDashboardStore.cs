using Npgsql;
using NpgsqlTypes;
using StudentConnect.Application;

namespace StudentConnect.Infrastructure.Data.Stores;

public sealed record AnalyticsDashboardFilter(
    int SchoolId,
    int? TeacherId,
    int? AssignmentId,
    DateOnly DateFrom,
    DateOnly DateTo,
    double AttendanceThreshold,
    double MarksThreshold);

public class AnalyticsDashboardStore(Database db)
{
    private const string ScopeCte = """
        scope_assignments AS (
            SELECT tsa."Id", tsa."TeacherId", tsa."SubjectId", tsa."SectionId"
            FROM "TeacherSubjectAssignments" tsa
            JOIN "Sections" sec ON sec."Id" = tsa."SectionId"
            JOIN "Grades" g ON g."Id" = sec."GradeId"
            WHERE g."SchoolId" = @schoolId
              AND (@teacherId IS NULL OR tsa."TeacherId" = @teacherId)
              AND (@assignmentId IS NULL OR tsa."Id" = @assignmentId)
        ),
        scope_students AS (
            SELECT s."Id", s."StudentId", s."FirstName", s."LastName", s."GradeId", s."SectionId",
                   g."Name" AS "GradeName", COALESCE(sec."Name", '') AS "SectionName"
            FROM "Students" s
            JOIN "Grades" g ON g."Id" = s."GradeId"
            LEFT JOIN "Sections" sec ON sec."Id" = s."SectionId"
            WHERE s."SchoolId" = @schoolId AND s."Status" = 'Active'
                            AND ((@teacherId IS NULL AND @assignmentId IS NULL)
                                OR EXISTS (
                                        SELECT 1 FROM scope_assignments sa
                                        JOIN "StudentSubjects" ss ON ss."StudentId" = s."Id" AND ss."SubjectId" = sa."SubjectId"
                                        WHERE sa."SectionId" = s."SectionId"))
        )
        """;

    public async Task<AnalyticsDashboardDto> GetDashboardAsync(AnalyticsDashboardFilter filter)
    {
        var overviewTask = GetOverviewAsync(filter);
        var attendanceTask = GetAttendanceByGradeAsync(filter);
        var marksTask = GetMarksByGradeAsync(filter);
        var atRiskTask = GetAtRiskStudentsAsync(filter);
        var trendTask = GetTrendAsync(filter);
        await Task.WhenAll(overviewTask, attendanceTask, marksTask, atRiskTask, trendTask);

        return new AnalyticsDashboardDto
        {
            Overview = await overviewTask,
            AttendanceByGrade = await attendanceTask,
            MarksByGrade = await marksTask,
            AtRiskStudents = await atRiskTask,
            Trend = await trendTask,
            DateFrom = filter.DateFrom,
            DateTo = filter.DateTo,
            AttendanceThreshold = filter.AttendanceThreshold,
            MarksThreshold = filter.MarksThreshold,
            GeneratedAtUtc = DateTime.UtcNow,
        };
    }

    private async Task<AnalyticsOverviewDto> GetOverviewAsync(AnalyticsDashboardFilter filter)
    {
        var row = await db.QuerySingleOrDefaultAsync(
            $"""
            WITH {ScopeCte}
            SELECT
                (SELECT COUNT(*) FROM scope_students) AS "TotalStudents",
                CASE WHEN @teacherId IS NULL AND @assignmentId IS NULL
                    THEN (SELECT COUNT(*) FROM "Teachers" WHERE "SchoolId" = @schoolId)
                    ELSE (SELECT COUNT(DISTINCT "TeacherId") FROM scope_assignments) END AS "TotalTeachers",
                CASE WHEN @teacherId IS NULL AND @assignmentId IS NULL
                    THEN (SELECT COUNT(*) FROM "Subjects" WHERE "SchoolId" = @schoolId)
                    ELSE (SELECT COUNT(DISTINCT "SubjectId") FROM scope_assignments) END AS "TotalSubjects",
                (SELECT COUNT(*) FROM "Attendance" a JOIN scope_students s ON s."Id" = a."StudentId"
                    WHERE a."Date" BETWEEN @dateFrom AND @dateTo
                      AND ((@teacherId IS NULL AND @assignmentId IS NULL) OR EXISTS (
                        SELECT 1 FROM scope_assignments sa WHERE sa."SectionId" = s."SectionId" AND sa."SubjectId" = a."SubjectId"))) AS "AttendanceTotal",
                (SELECT COUNT(*) FROM "Attendance" a JOIN scope_students s ON s."Id" = a."StudentId"
                    WHERE a."Date" BETWEEN @dateFrom AND @dateTo AND a."Status" = 'Present'
                      AND ((@teacherId IS NULL AND @assignmentId IS NULL) OR EXISTS (
                        SELECT 1 FROM scope_assignments sa WHERE sa."SectionId" = s."SectionId" AND sa."SubjectId" = a."SubjectId"))) AS "AttendancePresent",
                (SELECT COUNT(*) FROM "Marks" m JOIN "Assessments" a ON a."Id" = m."AssessmentId" JOIN scope_students s ON s."Id" = m."StudentId"
                    WHERE a."Date" BETWEEN @dateFrom AND @dateTo AND a."Status" = 'Published'
                      AND ((@teacherId IS NULL AND @assignmentId IS NULL) OR EXISTS (
                        SELECT 1 FROM scope_assignments sa WHERE sa."SectionId" = s."SectionId" AND sa."SubjectId" = a."SubjectId"))) AS "MarksCount",
                (SELECT COALESCE(SUM(m."MarksObtained" * 100.0 / NULLIF(a."MaximumMarks", 0)), 0)::double precision
                    FROM "Marks" m JOIN "Assessments" a ON a."Id" = m."AssessmentId" JOIN scope_students s ON s."Id" = m."StudentId"
                    WHERE a."Date" BETWEEN @dateFrom AND @dateTo AND a."Status" = 'Published'
                      AND ((@teacherId IS NULL AND @assignmentId IS NULL) OR EXISTS (
                        SELECT 1 FROM scope_assignments sa WHERE sa."SectionId" = s."SectionId" AND sa."SubjectId" = a."SubjectId"))) AS "MarksPercentageSum"
            """,
            p => Bind(p, filter),
            reader => new
            {
                TotalStudents = (int)reader.GetInt64("TotalStudents"),
                TotalTeachers = (int)reader.GetInt64("TotalTeachers"),
                TotalSubjects = (int)reader.GetInt64("TotalSubjects"),
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
            OverallAttendancePercentage = row.AttendanceTotal == 0 ? 0 : Math.Round(row.AttendancePresent * 100.0 / row.AttendanceTotal, 1),
            AverageMarksPercentage = row.MarksCount == 0 ? 0 : Math.Round(row.MarksPercentageSum / row.MarksCount, 1),
        };
    }

    private async Task<List<GradeAttendanceDto>> GetAttendanceByGradeAsync(AnalyticsDashboardFilter filter) =>
        await db.QueryAsync(
            $"""
            WITH {ScopeCte}
            SELECT s."GradeId", s."GradeName", COUNT(*) AS "Total", COUNT(*) FILTER (WHERE a."Status" = 'Present') AS "Present"
            FROM "Attendance" a
            JOIN scope_students s ON s."Id" = a."StudentId"
            WHERE a."Date" BETWEEN @dateFrom AND @dateTo
              AND ((@teacherId IS NULL AND @assignmentId IS NULL) OR EXISTS (
                SELECT 1 FROM scope_assignments sa WHERE sa."SectionId" = s."SectionId" AND sa."SubjectId" = a."SubjectId"))
            GROUP BY s."GradeId", s."GradeName"
            ORDER BY s."GradeName"
            """,
            p => Bind(p, filter),
            reader =>
            {
                var total = reader.GetInt64("Total");
                var present = reader.GetInt64("Present");
                return new GradeAttendanceDto
                {
                    GradeId = reader.GetInt32("GradeId"),
                    GradeName = reader.GetString("GradeName"),
                    TotalRecords = (int)total,
                    AttendancePercentage = total == 0 ? 0 : Math.Round(present * 100.0 / total, 1),
                };
            });

    private async Task<List<GradeMarksDto>> GetMarksByGradeAsync(AnalyticsDashboardFilter filter) =>
        await db.QueryAsync(
            $"""
            WITH {ScopeCte}
            SELECT s."GradeId", s."GradeName", AVG(m."MarksObtained" * 100.0 / NULLIF(a."MaximumMarks", 0))::double precision AS "AveragePercentage"
            FROM "Marks" m
            JOIN "Assessments" a ON a."Id" = m."AssessmentId"
            JOIN scope_students s ON s."Id" = m."StudentId"
            WHERE a."Date" BETWEEN @dateFrom AND @dateTo AND a."Status" = 'Published'
              AND ((@teacherId IS NULL AND @assignmentId IS NULL) OR EXISTS (
                SELECT 1 FROM scope_assignments sa WHERE sa."SectionId" = s."SectionId" AND sa."SubjectId" = a."SubjectId"))
            GROUP BY s."GradeId", s."GradeName"
            ORDER BY s."GradeName"
            """,
            p => Bind(p, filter),
            reader => new GradeMarksDto
            {
                GradeId = reader.GetInt32("GradeId"),
                GradeName = reader.GetString("GradeName"),
                AverageMarksPercentage = Math.Round(reader.GetDouble("AveragePercentage"), 1),
            });

    private async Task<List<AtRiskStudentDto>> GetAtRiskStudentsAsync(AnalyticsDashboardFilter filter)
    {
        var rows = await db.QueryAsync(
            $"""
            WITH {ScopeCte},
            attendance_stats AS (
                SELECT a."StudentId", COUNT(*) AS total, COUNT(*) FILTER (WHERE a."Status" = 'Present') AS present
                FROM "Attendance" a JOIN scope_students s ON s."Id" = a."StudentId"
                WHERE a."Date" BETWEEN @dateFrom AND @dateTo
                  AND ((@teacherId IS NULL AND @assignmentId IS NULL) OR EXISTS (
                    SELECT 1 FROM scope_assignments sa WHERE sa."SectionId" = s."SectionId" AND sa."SubjectId" = a."SubjectId"))
                GROUP BY a."StudentId"
            ),
            marks_stats AS (
                SELECT m."StudentId", AVG(m."MarksObtained" * 100.0 / NULLIF(a."MaximumMarks", 0)) AS avg_pct
                FROM "Marks" m JOIN "Assessments" a ON a."Id" = m."AssessmentId" JOIN scope_students s ON s."Id" = m."StudentId"
                WHERE a."Date" BETWEEN @dateFrom AND @dateTo AND a."Status" = 'Published'
                  AND ((@teacherId IS NULL AND @assignmentId IS NULL) OR EXISTS (
                    SELECT 1 FROM scope_assignments sa WHERE sa."SectionId" = s."SectionId" AND sa."SubjectId" = a."SubjectId"))
                GROUP BY m."StudentId"
            )
            SELECT s."Id", s."StudentId" AS "StudentCode", s."FirstName", s."LastName", s."GradeName", s."SectionName",
                   CASE WHEN att.total > 0 THEN ROUND((att.present * 100.0 / att.total)::numeric, 1)::double precision END AS "AttendancePercentage",
                   CASE WHEN ms.avg_pct IS NOT NULL THEN ROUND(ms.avg_pct::numeric, 1)::double precision END AS "MarksPercentage"
            FROM scope_students s
            LEFT JOIN attendance_stats att ON att."StudentId" = s."Id"
            LEFT JOIN marks_stats ms ON ms."StudentId" = s."Id"
            WHERE (att.total > 0 AND att.present * 100.0 / att.total < @attendanceThreshold)
               OR (ms.avg_pct IS NOT NULL AND ms.avg_pct < @marksThreshold)
            ORDER BY LEAST(COALESCE(att.present * 100.0 / NULLIF(att.total, 0), 100), COALESCE(ms.avg_pct, 100)), s."LastName"
            """,
            p => Bind(p, filter),
            reader => new AtRiskStudentDto
            {
                Id = reader.GetInt32("Id"),
                StudentId = reader.GetString("StudentCode"),
                FullName = $"{reader.GetString("FirstName")} {reader.GetString("LastName")}",
                GradeName = reader.GetString("GradeName"),
                SectionName = reader.GetString("SectionName"),
                AttendancePercentage = reader.IsDBNull(reader.GetOrdinal("AttendancePercentage")) ? null : reader.GetDouble("AttendancePercentage"),
                AverageMarksPercentage = reader.IsDBNull(reader.GetOrdinal("MarksPercentage")) ? null : reader.GetDouble("MarksPercentage"),
            });

        foreach (var row in rows)
        {
            var attendanceRisk = row.AttendancePercentage < filter.AttendanceThreshold;
            var marksRisk = row.AverageMarksPercentage < filter.MarksThreshold;
            if (attendanceRisk) row.Reasons.Add($"Attendance below {filter.AttendanceThreshold:0.#}%");
            if (marksRisk) row.Reasons.Add($"Marks below {filter.MarksThreshold:0.#}%");
            var lowestMargin = Math.Min(
                row.AttendancePercentage is null ? 100 : row.AttendancePercentage.Value - filter.AttendanceThreshold,
                row.AverageMarksPercentage is null ? 100 : row.AverageMarksPercentage.Value - filter.MarksThreshold);
            row.RiskLevel = attendanceRisk && marksRisk || lowestMargin <= -15 ? "High" : "Watch";
        }
        return rows;
    }

    private async Task<List<MonthlyTrendDto>> GetTrendAsync(AnalyticsDashboardFilter filter) =>
        await db.QueryAsync(
            $"""
            WITH {ScopeCte},
            attendance_monthly AS (
                SELECT date_trunc('month', a."Date"::timestamp) AS month, COUNT(*) AS total,
                       COUNT(*) FILTER (WHERE a."Status" = 'Present') AS present
                FROM "Attendance" a JOIN scope_students s ON s."Id" = a."StudentId"
                WHERE a."Date" BETWEEN @dateFrom AND @dateTo
                  AND ((@teacherId IS NULL AND @assignmentId IS NULL) OR EXISTS (
                    SELECT 1 FROM scope_assignments sa WHERE sa."SectionId" = s."SectionId" AND sa."SubjectId" = a."SubjectId"))
                GROUP BY month
            ),
            marks_monthly AS (
                SELECT date_trunc('month', a."Date"::timestamp) AS month, SUM(m."MarksObtained") AS obtained, SUM(a."MaximumMarks") AS maximum
                FROM "Marks" m JOIN "Assessments" a ON a."Id" = m."AssessmentId" JOIN scope_students s ON s."Id" = m."StudentId"
                WHERE a."Date" BETWEEN @dateFrom AND @dateTo AND a."Status" = 'Published'
                  AND ((@teacherId IS NULL AND @assignmentId IS NULL) OR EXISTS (
                    SELECT 1 FROM scope_assignments sa WHERE sa."SectionId" = s."SectionId" AND sa."SubjectId" = a."SubjectId"))
                GROUP BY month
            )
            SELECT COALESCE(am.month, mm.month) AS "Month", COALESCE(am.total, 0) AS "Total",
                   COALESCE(am.present, 0) AS "Present", COALESCE(mm.obtained, 0) AS "Obtained", COALESCE(mm.maximum, 0) AS "Maximum"
            FROM attendance_monthly am FULL OUTER JOIN marks_monthly mm ON am.month = mm.month
            ORDER BY "Month"
            """,
            p => Bind(p, filter),
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

    private static void Bind(NpgsqlParameterCollection parameters, AnalyticsDashboardFilter filter)
    {
        parameters.AddWithValue("schoolId", filter.SchoolId);
        parameters.Add("teacherId", NpgsqlDbType.Integer).Value = filter.TeacherId is { } teacherId ? teacherId : DBNull.Value;
        parameters.Add("assignmentId", NpgsqlDbType.Integer).Value = filter.AssignmentId is { } assignmentId ? assignmentId : DBNull.Value;
        parameters.AddWithValue("dateFrom", filter.DateFrom);
        parameters.AddWithValue("dateTo", filter.DateTo);
        parameters.AddWithValue("attendanceThreshold", filter.AttendanceThreshold);
        parameters.AddWithValue("marksThreshold", filter.MarksThreshold);
    }
}