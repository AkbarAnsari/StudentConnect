using StudentConnect.Application;

namespace StudentConnect.Infrastructure.Data.Stores;

public class SetupDependencyStore(Database db)
{
    public async Task<List<SetupDependencyDto>> GetAllAsync(int schoolId) =>
        await db.QueryAsync(
            """
            SELECT 'Grade' AS "ResourceType", g."Id" AS "ResourceId",
                   (SELECT COUNT(*) FROM "Students" s WHERE s."GradeId" = g."Id") AS "Students",
                   (SELECT COUNT(*) FROM "Sections" sec WHERE sec."GradeId" = g."Id") AS "Sections",
                   (SELECT COUNT(*) FROM "Assessments" a JOIN "TeacherSubjectAssignments" tsa ON tsa."Id" = a."TeacherSubjectAssignmentId" JOIN "Sections" sec ON sec."Id" = tsa."SectionId" WHERE sec."GradeId" = g."Id") AS "Assessments",
                   ((SELECT COUNT(*) FROM "Homework" h WHERE h."GradeId" = g."Id") + (SELECT COUNT(*) FROM "LearningMaterials" lm WHERE lm."GradeId" = g."Id") + (SELECT COUNT(*) FROM "Activities" act WHERE act."GradeId" = g."Id")) AS "Content",
                   (SELECT COUNT(*) FROM "TeacherSubjectAssignments" tsa JOIN "Sections" sec ON sec."Id" = tsa."SectionId" WHERE sec."GradeId" = g."Id") AS "Assignments",
                   0::bigint AS "IssuedReports"
            FROM "Grades" g WHERE g."SchoolId" = @schoolId
            UNION ALL
            SELECT 'Subject', sub."Id",
                   (SELECT COUNT(*) FROM "StudentSubjects" ss WHERE ss."SubjectId" = sub."Id"),
                   0::bigint,
                   (SELECT COUNT(*) FROM "Assessments" a WHERE a."SubjectId" = sub."Id"),
                   ((SELECT COUNT(*) FROM "Homework" h WHERE h."SubjectId" = sub."Id") + (SELECT COUNT(*) FROM "LearningMaterials" lm WHERE lm."SubjectId" = sub."Id")),
                   (SELECT COUNT(*) FROM "TeacherSubjectAssignments" tsa WHERE tsa."SubjectId" = sub."Id"),
                   0::bigint
            FROM "Subjects" sub WHERE sub."SchoolId" = @schoolId
            UNION ALL
            SELECT 'Section', sec."Id",
                   (SELECT COUNT(*) FROM "Students" s WHERE s."SectionId" = sec."Id"),
                   0::bigint,
                   (SELECT COUNT(*) FROM "Assessments" a JOIN "TeacherSubjectAssignments" tsa ON tsa."Id" = a."TeacherSubjectAssignmentId" WHERE tsa."SectionId" = sec."Id"),
                   ((SELECT COUNT(*) FROM "Homework" h WHERE h."SectionId" = sec."Id") + (SELECT COUNT(*) FROM "LearningMaterials" lm WHERE lm."SectionId" = sec."Id") + (SELECT COUNT(*) FROM "Activities" act WHERE act."SectionId" = sec."Id")),
                   (SELECT COUNT(*) FROM "TeacherSubjectAssignments" tsa WHERE tsa."SectionId" = sec."Id"),
                   0::bigint
            FROM "Sections" sec JOIN "Grades" g ON g."Id" = sec."GradeId" WHERE g."SchoolId" = @schoolId
            UNION ALL
            SELECT 'Term', t."Id", 0::bigint, 0::bigint,
                   (SELECT COUNT(*) FROM "Assessments" a WHERE a."TermId" = t."Id"),
                   0::bigint, 0::bigint,
                   (SELECT COUNT(*) FROM "ReportIssuances" ri WHERE ri."TermId" = t."Id")
            FROM "Terms" t JOIN "AcademicYears" ay ON ay."Id" = t."AcademicYearId" WHERE ay."SchoolId" = @schoolId
            ORDER BY "ResourceType", "ResourceId"
            """,
            p => p.AddWithValue("schoolId", schoolId),
            reader => new SetupDependencyDto
            {
                ResourceType = reader.GetString("ResourceType"), ResourceId = reader.GetInt32("ResourceId"),
                StudentCount = (int)reader.GetInt64("Students"), SectionCount = (int)reader.GetInt64("Sections"),
                AssessmentCount = (int)reader.GetInt64("Assessments"), ContentCount = (int)reader.GetInt64("Content"),
                AssignmentCount = (int)reader.GetInt64("Assignments"), IssuedReportCount = (int)reader.GetInt64("IssuedReports"),
            });
}