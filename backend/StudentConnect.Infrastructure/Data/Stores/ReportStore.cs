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

    public async Task<StudentReportCardDto?> GetStudentReportCardAsync(int studentId, int schoolId, DateOnly? dateFrom = null, DateOnly? dateTo = null)
    {
        var periodStart = dateFrom ?? new DateOnly(1900, 1, 1);
        var periodEnd = dateTo ?? new DateOnly(2999, 12, 31);
        var identity = await db.QuerySingleOrDefaultAsync(
            """
            SELECT s."Id", s."StudentId" AS "StudentCode", s."FirstName", s."LastName", s."GradeId", s."SectionId",
                   g."Name" AS "GradeName", COALESCE(sec."Name", '') AS "SectionName"
            FROM "Students" s
            JOIN "Grades" g ON g."Id" = s."GradeId"
            LEFT JOIN "Sections" sec ON sec."Id" = s."SectionId"
            WHERE s."Id" = @studentId AND s."SchoolId" = @schoolId
            """,
            p =>
            {
                p.AddWithValue("studentId", studentId);
                p.AddWithValue("schoolId", schoolId);
            },
            reader => new
            {
                Id = reader.GetInt32("Id"),
                StudentCode = reader.GetString("StudentCode"),
                FullName = $"{reader.GetString("FirstName")} {reader.GetString("LastName")}",
                GradeId = reader.GetInt32("GradeId"),
                SectionId = reader.GetNullableInt32("SectionId"),
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
                FROM "Attendance" WHERE "StudentId" = @studentId AND "Date" BETWEEN @dateFrom AND @dateTo GROUP BY "SubjectId"
            ),
            marks_agg AS (
                SELECT asm."SubjectId", SUM(m."MarksObtained") AS obtained, SUM(asm."MaximumMarks") AS maximum
                FROM "Marks" m JOIN "Assessments" asm ON asm."Id" = m."AssessmentId"
                WHERE m."StudentId" = @studentId AND asm."Status" = 'Published' AND asm."Date" BETWEEN @dateFrom AND @dateTo GROUP BY asm."SubjectId"
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
            p =>
            {
                p.AddWithValue("studentId", studentId);
                p.AddWithValue("dateFrom", periodStart);
                p.AddWithValue("dateTo", periodEnd);
            },
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
                WHERE a."Status" = 'Published' AND a."Date" BETWEEN @dateFrom AND @dateTo
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
                p.AddWithValue("dateFrom", periodStart);
                p.AddWithValue("dateTo", periodEnd);
            },
            reader => new { Rank = reader.GetInt32("Rank"), TotalStudents = (int)reader.GetInt64("TotalStudents") });

        var homework = await db.QuerySingleOrDefaultAsync(
            """
            SELECT
                (SELECT COUNT(*) FROM "Homework" WHERE "GradeId" = @gradeId AND ("SectionId" IS NULL OR "SectionId" = @sectionId)
                    AND "DueDate"::date BETWEEN @dateFrom AND @dateTo
                    AND ("Status" IN ('Published', 'Closed') OR ("Status" = 'Scheduled' AND "ScheduledForUtc" <= now()))) AS "TotalAssigned",
                (SELECT COUNT(*) FROM "HomeworkSubmissions" hs JOIN "Homework" h ON h."Id" = hs."HomeworkId"
                    WHERE hs."StudentId" = @studentId AND h."GradeId" = @gradeId AND (h."SectionId" IS NULL OR h."SectionId" = @sectionId)
                    AND h."DueDate"::date BETWEEN @dateFrom AND @dateTo) AS "Submitted"
            """,
            p =>
            {
                p.AddWithValue("gradeId", identity.GradeId);
                p.AddWithValue("sectionId", (object?)identity.SectionId ?? DBNull.Value);
                p.AddWithValue("studentId", studentId);
                p.AddWithValue("dateFrom", periodStart);
                p.AddWithValue("dateTo", periodEnd);
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
            PeriodStartDate = dateFrom,
            PeriodEndDate = dateTo,
            GeneratedAtUtc = DateTime.UtcNow,
        };
    }

    // Groups every recorded Mark by the AcademicYear whose date range contains the Assessment's
    // date — Assessments have no direct AcademicYear/Term FK, so this is resolved by date range
    // instead of a join column. SchoolId-scoped so a date range never matches another tenant's year.
    public async Task<StudentTranscriptDto?> GetStudentTranscriptAsync(int studentId, int schoolId)
    {
        var identity = await db.QuerySingleOrDefaultAsync(
            """
            SELECT s."Id", s."StudentId" AS "StudentCode", s."FirstName", s."LastName"
            FROM "Students" s
            WHERE s."Id" = @studentId AND s."SchoolId" = @schoolId
            """,
            p =>
            {
                p.AddWithValue("studentId", studentId);
                p.AddWithValue("schoolId", schoolId);
            },
            reader => new
            {
                Id = reader.GetInt32("Id"),
                StudentCode = reader.GetString("StudentCode"),
                FullName = $"{reader.GetString("FirstName")} {reader.GetString("LastName")}",
            });

        if (identity is null)
        {
            return null;
        }

        var rows = await db.QueryAsync(
            """
            SELECT ay."Id" AS "AcademicYearId", ay."Name" AS "AcademicYearName", ay."StartDate",
                   asm."SubjectId", sub."Name" AS "SubjectName",
                   SUM(m."MarksObtained") AS "TotalObtained", SUM(asm."MaximumMarks") AS "TotalMaximum"
            FROM "Marks" m
            JOIN "Assessments" asm ON asm."Id" = m."AssessmentId"
            JOIN "Subjects" sub ON sub."Id" = asm."SubjectId"
            JOIN "AcademicYears" ay ON asm."Date" BETWEEN ay."StartDate" AND ay."EndDate" AND ay."SchoolId" = @schoolId
            WHERE m."StudentId" = @studentId AND asm."Status" = 'Published'
            GROUP BY ay."Id", ay."Name", ay."StartDate", asm."SubjectId", sub."Name"
            ORDER BY ay."StartDate", sub."Name"
            """,
            p =>
            {
                p.AddWithValue("studentId", studentId);
                p.AddWithValue("schoolId", schoolId);
            },
            reader => new
            {
                AcademicYearId = reader.GetInt32("AcademicYearId"),
                AcademicYearName = reader.GetString("AcademicYearName"),
                SubjectId = reader.GetInt32("SubjectId"),
                SubjectName = reader.GetString("SubjectName"),
                TotalObtained = (int)reader.GetInt64("TotalObtained"),
                TotalMaximum = (int)reader.GetInt64("TotalMaximum"),
            });

        var years = rows
            .GroupBy(r => (r.AcademicYearId, r.AcademicYearName))
            .Select(yearGroup =>
            {
                var subjectRows = yearGroup.Select(r => new TranscriptSubjectRowDto
                {
                    SubjectId = r.SubjectId,
                    SubjectName = r.SubjectName,
                    MarksPercentage = r.TotalMaximum == 0 ? 0 : Math.Round(r.TotalObtained * 100.0 / r.TotalMaximum, 1),
                }).ToList();
                foreach (var row in subjectRows)
                {
                    row.ResultCategory = ResultCategory.From(row.MarksPercentage);
                }

                var yearObtained = yearGroup.Sum(r => r.TotalObtained);
                var yearMaximum = yearGroup.Sum(r => r.TotalMaximum);
                var yearPercentage = yearMaximum == 0 ? 0 : Math.Round(yearObtained * 100.0 / yearMaximum, 1);

                return new TranscriptYearRowDto
                {
                    AcademicYearId = yearGroup.Key.AcademicYearId,
                    AcademicYearName = yearGroup.Key.AcademicYearName,
                    OverallMarksPercentage = yearPercentage,
                    OverallResultCategory = ResultCategory.From(yearPercentage),
                    Subjects = subjectRows,
                };
            })
            .ToList();

        return new StudentTranscriptDto
        {
            StudentId = identity.Id,
            StudentCode = identity.StudentCode,
            FullName = identity.FullName,
            Years = years,
        };
    }

    public async Task<List<SectionClassReportDto>> GetTeacherClassReportsAsync(int teacherId, int schoolId) =>
        await db.QueryAsync(
            """
            WITH student_attendance AS (
                SELECT "StudentId", "SubjectId", COUNT(*) AS total, COUNT(*) FILTER (WHERE "Status" = 'Present') AS present
                FROM "Attendance" GROUP BY "StudentId", "SubjectId"
            ),
            student_marks AS (
                SELECT m."StudentId", a."SubjectId", AVG(m."MarksObtained" * 100.0 / a."MaximumMarks") AS avg_pct
                FROM "Marks" m JOIN "Assessments" a ON a."Id" = m."AssessmentId"
                WHERE a."Status" = 'Published'
                GROUP BY m."StudentId", a."SubjectId"
            )
            SELECT
                tsa."Id" AS "AssignmentId", sub."Id" AS "SubjectId", sub."Name" AS "SubjectName",
                sec."Id" AS "SectionId", g."Name" AS "GradeName", sec."Name" AS "SectionName",
                COUNT(s."Id") AS "StudentCount",
                AVG(CASE WHEN sa.total > 0 THEN sa.present * 100.0 / sa.total END) AS "AvgAttendance",
                AVG(sm.avg_pct) AS "AvgMarks",
                COUNT(*) FILTER (
                    WHERE (sa.total > 0 AND sa.present * 100.0 / sa.total < @attendanceThreshold)
                       OR (sm.avg_pct IS NOT NULL AND sm.avg_pct < @marksThreshold)
                ) AS "AtRiskCount"
            FROM "TeacherSubjectAssignments" tsa
            JOIN "Subjects" sub ON sub."Id" = tsa."SubjectId"
            JOIN "Sections" sec ON sec."Id" = tsa."SectionId"
            JOIN "Grades" g ON g."Id" = sec."GradeId"
            LEFT JOIN "Students" s ON s."SectionId" = sec."Id"
                AND s."SchoolId" = @schoolId
                AND s."Status" = 'Active'
                AND EXISTS (
                    SELECT 1 FROM "StudentSubjects" ss
                    WHERE ss."StudentId" = s."Id" AND ss."SubjectId" = tsa."SubjectId"
                )
            LEFT JOIN student_attendance sa ON sa."StudentId" = s."Id" AND sa."SubjectId" = tsa."SubjectId"
            LEFT JOIN student_marks sm ON sm."StudentId" = s."Id" AND sm."SubjectId" = tsa."SubjectId"
            WHERE tsa."TeacherId" = @teacherId AND g."SchoolId" = @schoolId
            GROUP BY tsa."Id", sub."Id", sub."Name", sec."Id", g."Name", sec."Name"
            ORDER BY g."Name", sec."Name", sub."Name"
            """,
            p =>
            {
                p.AddWithValue("teacherId", teacherId);
                p.AddWithValue("schoolId", schoolId);
                p.AddWithValue("attendanceThreshold", AtRiskAttendanceThreshold);
                p.AddWithValue("marksThreshold", AtRiskMarksThreshold);
            },
            reader => new SectionClassReportDto
            {
                AssignmentId = reader.GetInt32("AssignmentId"),
                SubjectId = reader.GetInt32("SubjectId"),
                SubjectName = reader.GetString("SubjectName"),
                SectionId = reader.GetInt32("SectionId"),
                GradeName = reader.GetString("GradeName"),
                SectionName = reader.GetString("SectionName"),
                StudentCount = (int)reader.GetInt64("StudentCount"),
                AverageAttendancePercentage = reader.IsDBNull(reader.GetOrdinal("AvgAttendance")) ? 0 : Math.Round(reader.GetDouble("AvgAttendance"), 1),
                AverageMarksPercentage = reader.IsDBNull(reader.GetOrdinal("AvgMarks")) ? 0 : Math.Round(reader.GetDouble("AvgMarks"), 1),
                AtRiskCount = (int)reader.GetInt64("AtRiskCount"),
            });
}
