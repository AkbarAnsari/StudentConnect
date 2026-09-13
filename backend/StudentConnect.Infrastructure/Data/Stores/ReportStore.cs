using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;

namespace StudentConnect.Infrastructure.Data.Stores;

// Consolidated reporting queries for the per-student report card and per-teacher class
// report — each sub-query pre-aggregates in its own CTE before joining, to avoid the
// classic fan-out bug of joining two one-to-many tables (Attendance, Marks) together
// in a single GROUP BY.
public class ReportStore(Database db)
{
    private const double AtRiskAttendanceThreshold = 75;
    private const double AtRiskMarksThreshold = 40;

    public async Task<StudentReportCardDto?> GetStudentReportCardAsync(int studentId)
    {
        var identity = await db.QuerySingleOrDefaultAsync(
            """
            SELECT s."Id", s."StudentId" AS "StudentCode", s."FirstName", s."LastName", s."GradeId",
                   g."Name" AS "GradeName", COALESCE(sec."Name", '') AS "SectionName"
            FROM "Students" s
            JOIN "Grades" g ON g."Id" = s."GradeId"
            LEFT JOIN "Sections" sec ON sec."Id" = s."SectionId"
            WHERE s."Id" = @studentId
            """,
            p => p.AddWithValue("studentId", studentId),
            reader => new
            {
                Id = reader.GetInt32("Id"),
                StudentCode = reader.GetString("StudentCode"),
                FullName = $"{reader.GetString("FirstName")} {reader.GetString("LastName")}",
                GradeId = reader.GetInt32("GradeId"),
                GradeName = reader.GetString("GradeName"),
                SectionName = reader.GetString("SectionName"),
            });

        if (identity is null)
        {
            return null;
        }

        var subjects = await db.QueryAsync(
            """
            WITH attendance_agg AS (
                SELECT "SubjectId", COUNT(*) AS total, COUNT(*) FILTER (WHERE "Status" = 'Present') AS present
                FROM "Attendance" WHERE "StudentId" = @studentId GROUP BY "SubjectId"
            ),
            marks_agg AS (
                SELECT asm."SubjectId", SUM(m."MarksObtained") AS obtained, SUM(asm."MaximumMarks") AS maximum
                FROM "Marks" m JOIN "Assessments" asm ON asm."Id" = m."AssessmentId"
                WHERE m."StudentId" = @studentId GROUP BY asm."SubjectId"
            )
            SELECT ss."SubjectId", sub."Name" AS "SubjectName",
                   COALESCE(att.total, 0) AS "TotalClasses", COALESCE(att.present, 0) AS "Present",
                   COALESCE(ma.obtained, 0) AS "TotalMarksObtained", COALESCE(ma.maximum, 0) AS "TotalMaximumMarks"
            FROM "StudentSubjects" ss
            JOIN "Subjects" sub ON sub."Id" = ss."SubjectId"
            LEFT JOIN attendance_agg att ON att."SubjectId" = ss."SubjectId"
            LEFT JOIN marks_agg ma ON ma."SubjectId" = ss."SubjectId"
            WHERE ss."StudentId" = @studentId
            ORDER BY sub."Name"
            """,
            p => p.AddWithValue("studentId", studentId),
            reader => new
            {
                SubjectId = reader.GetInt32("SubjectId"),
                SubjectName = reader.GetString("SubjectName"),
                TotalClasses = (int)reader.GetInt64("TotalClasses"),
                Present = (int)reader.GetInt64("Present"),
                TotalMarksObtained = (int)reader.GetInt64("TotalMarksObtained"),
                TotalMaximumMarks = (int)reader.GetInt64("TotalMaximumMarks"),
            });

        var subjectRows = subjects.Select(row => new SubjectReportRowDto
        {
            SubjectId = row.SubjectId,
            SubjectName = row.SubjectName,
            AttendancePercentage = row.TotalClasses == 0 ? 0 : Math.Round(row.Present * 100.0 / row.TotalClasses, 1),
            MarksPercentage = row.TotalMaximumMarks == 0 ? 0 : Math.Round(row.TotalMarksObtained * 100.0 / row.TotalMaximumMarks, 1),
        }).ToList();
        foreach (var row in subjectRows)
        {
            row.ResultCategory = ResultCategory.From(row.MarksPercentage);
        }

        var totalClassesAll = subjects.Sum(r => r.TotalClasses);
        var presentAll = subjects.Sum(r => r.Present);
        var marksObtainedAll = subjects.Sum(r => r.TotalMarksObtained);
        var maxMarksAll = subjects.Sum(r => r.TotalMaximumMarks);
        var overallAttendance = totalClassesAll == 0 ? 0 : Math.Round(presentAll * 100.0 / totalClassesAll, 1);
        var overallMarks = maxMarksAll == 0 ? 0 : Math.Round(marksObtainedAll * 100.0 / maxMarksAll, 1);

        var rank = await db.QuerySingleOrDefaultAsync(
            """
            WITH student_avg AS (
                SELECT m."StudentId", AVG(m."MarksObtained" * 100.0 / a."MaximumMarks") AS avg_pct
                FROM "Marks" m JOIN "Assessments" a ON a."Id" = m."AssessmentId"
                GROUP BY m."StudentId"
            ),
            grade_students AS (
                SELECT "Id" FROM "Students" WHERE "GradeId" = @gradeId
            ),
            ranked AS (
                SELECT gs."Id" AS "StudentId",
                       RANK() OVER (ORDER BY COALESCE(sa.avg_pct, 0) DESC) AS "Rank",
                       COUNT(*) OVER () AS "TotalStudents"
                FROM grade_students gs
                LEFT JOIN student_avg sa ON sa."StudentId" = gs."Id"
            )
            SELECT "Rank", "TotalStudents" FROM ranked WHERE "StudentId" = @studentId
            """,
            p =>
            {
                p.AddWithValue("gradeId", identity.GradeId);
                p.AddWithValue("studentId", studentId);
            },
            reader => new { Rank = reader.GetInt32("Rank"), TotalStudents = (int)reader.GetInt64("TotalStudents") });

        var homework = await db.QuerySingleOrDefaultAsync(
            """
            SELECT
                (SELECT COUNT(*) FROM "Homework" WHERE "GradeId" = @gradeId) AS "TotalAssigned",
                (SELECT COUNT(*) FROM "HomeworkSubmissions" hs JOIN "Homework" h ON h."Id" = hs."HomeworkId"
                    WHERE hs."StudentId" = @studentId AND h."GradeId" = @gradeId) AS "Submitted"
            """,
            p =>
            {
                p.AddWithValue("gradeId", identity.GradeId);
                p.AddWithValue("studentId", studentId);
            },
            reader => new { TotalAssigned = (int)reader.GetInt64("TotalAssigned"), Submitted = (int)reader.GetInt64("Submitted") });

        return new StudentReportCardDto
        {
            StudentId = identity.Id,
            StudentCode = identity.StudentCode,
            FullName = identity.FullName,
            GradeName = identity.GradeName,
            SectionName = identity.SectionName,
            OverallAttendancePercentage = overallAttendance,
            OverallMarksPercentage = overallMarks,
            OverallResultCategory = ResultCategory.From(overallMarks),
            RankInGrade = rank is null || rank.TotalStudents == 0 ? 0 : rank.Rank,
            TotalStudentsInGrade = rank?.TotalStudents ?? 0,
            HomeworkCompletionRate = homework is null || homework.TotalAssigned == 0 ? 0 : Math.Round(homework.Submitted * 100.0 / homework.TotalAssigned, 1),
            Subjects = subjectRows,
        };
    }

    public async Task<List<SectionClassReportDto>> GetTeacherClassReportsAsync(int teacherId) =>
        await db.QueryAsync(
            """
            WITH student_attendance AS (
                SELECT "StudentId", COUNT(*) AS total, COUNT(*) FILTER (WHERE "Status" = 'Present') AS present
                FROM "Attendance" GROUP BY "StudentId"
            ),
            student_marks AS (
                SELECT m."StudentId", AVG(m."MarksObtained" * 100.0 / a."MaximumMarks") AS avg_pct
                FROM "Marks" m JOIN "Assessments" a ON a."Id" = m."AssessmentId"
                GROUP BY m."StudentId"
            )
            SELECT
                sec."Id" AS "SectionId", g."Name" AS "GradeName", sec."Name" AS "SectionName",
                COUNT(s."Id") AS "StudentCount",
                AVG(CASE WHEN sa.total > 0 THEN sa.present * 100.0 / sa.total END) AS "AvgAttendance",
                AVG(sm.avg_pct) AS "AvgMarks",
                COUNT(*) FILTER (
                    WHERE (sa.total > 0 AND sa.present * 100.0 / sa.total < @attendanceThreshold)
                       OR (sm.avg_pct IS NOT NULL AND sm.avg_pct < @marksThreshold)
                ) AS "AtRiskCount"
            FROM "Sections" sec
            JOIN "Grades" g ON g."Id" = sec."GradeId"
            LEFT JOIN "Students" s ON s."SectionId" = sec."Id"
            LEFT JOIN student_attendance sa ON sa."StudentId" = s."Id"
            LEFT JOIN student_marks sm ON sm."StudentId" = s."Id"
            WHERE sec."HomeroomTeacherId" = @teacherId
            GROUP BY sec."Id", g."Name", sec."Name"
            ORDER BY g."Name", sec."Name"
            """,
            p =>
            {
                p.AddWithValue("teacherId", teacherId);
                p.AddWithValue("attendanceThreshold", AtRiskAttendanceThreshold);
                p.AddWithValue("marksThreshold", AtRiskMarksThreshold);
            },
            reader => new SectionClassReportDto
            {
                SectionId = reader.GetInt32("SectionId"),
                GradeName = reader.GetString("GradeName"),
                SectionName = reader.GetString("SectionName"),
                StudentCount = (int)reader.GetInt64("StudentCount"),
                AverageAttendancePercentage = reader.IsDBNull(reader.GetOrdinal("AvgAttendance")) ? 0 : Math.Round(reader.GetDouble("AvgAttendance"), 1),
                AverageMarksPercentage = reader.IsDBNull(reader.GetOrdinal("AvgMarks")) ? 0 : Math.Round(reader.GetDouble("AvgMarks"), 1),
                AtRiskCount = (int)reader.GetInt64("AtRiskCount"),
            });
}
