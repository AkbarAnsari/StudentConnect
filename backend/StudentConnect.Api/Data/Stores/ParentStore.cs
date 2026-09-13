using StudentConnect.Api.Models;

namespace StudentConnect.Api.Data.Stores;

public class ParentStore(Database db)
{
    public async Task<List<ParentChildDto>> GetChildrenForParentAsync(int parentUserId) =>
        await db.QueryAsync(
            """
            SELECT s."Id", s."StudentId", s."FirstName", s."LastName", g."Name" AS "GradeName", COALESCE(sec."Name", '') AS "SectionName"
            FROM "ParentStudents" ps
            JOIN "Students" s ON s."Id" = ps."StudentId"
            JOIN "Grades" g ON g."Id" = s."GradeId"
            LEFT JOIN "Sections" sec ON sec."Id" = s."SectionId"
            WHERE ps."ParentUserId" = @parentUserId
            ORDER BY s."Id"
            """,
            p => p.AddWithValue("parentUserId", parentUserId),
            reader => new ParentChildDto
            {
                Id = reader.GetInt32("Id"),
                StudentId = reader.GetString("StudentId"),
                FirstName = reader.GetString("FirstName"),
                LastName = reader.GetString("LastName"),
                GradeName = reader.GetString("GradeName"),
                Section = reader.GetString("SectionName"),
            });

    public async Task<List<int>> GetChildStudentIdsAsync(int parentUserId) =>
        await db.QueryAsync(
            """SELECT "StudentId" FROM "ParentStudents" WHERE "ParentUserId" = @parentUserId""",
            p => p.AddWithValue("parentUserId", parentUserId),
            reader => reader.GetInt32("StudentId"));

    // Reverse lookup — used to resolve notification recipients for a single student event
    // (attendance marked absent, a mark recorded).
    public async Task<List<int>> GetParentUserIdsForStudentAsync(int studentId) =>
        await db.QueryAsync(
            """SELECT "ParentUserId" FROM "ParentStudents" WHERE "StudentId" = @studentId""",
            p => p.AddWithValue("studentId", studentId),
            reader => reader.GetInt32("ParentUserId"));

    // Bulk reverse lookup — used to resolve notification recipients across many students
    // at once (e.g. homework assigned to a whole grade) without one query per student.
    public async Task<List<int>> GetParentUserIdsForStudentsAsync(IReadOnlyCollection<int> studentIds)
    {
        if (studentIds.Count == 0)
        {
            return [];
        }

        return await db.QueryAsync(
            """SELECT DISTINCT "ParentUserId" FROM "ParentStudents" WHERE "StudentId" = ANY(@studentIds)""",
            p => p.AddWithValue("studentIds", studentIds.ToArray()),
            reader => reader.GetInt32("ParentUserId"));
    }

    public async Task<bool> LinkExistsAsync(int parentUserId, int studentId) =>
        await db.ExistsAsync(
            """SELECT 1 FROM "ParentStudents" WHERE "ParentUserId" = @parentUserId AND "StudentId" = @studentId""",
            p =>
            {
                p.AddWithValue("parentUserId", parentUserId);
                p.AddWithValue("studentId", studentId);
            });

    public async Task LinkChildAsync(int parentUserId, int studentId) =>
        await db.ExecuteAsync(
            """INSERT INTO "ParentStudents" ("ParentUserId", "StudentId") VALUES (@parentUserId, @studentId)""",
            p =>
            {
                p.AddWithValue("parentUserId", parentUserId);
                p.AddWithValue("studentId", studentId);
            });

    public async Task<int> UnlinkChildAsync(int parentUserId, int studentId) =>
        await db.ExecuteAsync(
            """DELETE FROM "ParentStudents" WHERE "ParentUserId" = @parentUserId AND "StudentId" = @studentId""",
            p =>
            {
                p.AddWithValue("parentUserId", parentUserId);
                p.AddWithValue("studentId", studentId);
            });

    // Every parent user with their linked children — used by the admin "manage parents" list.
    public async Task<List<(int ParentUserId, int StudentId, string StudentCode, string FirstName, string LastName, string GradeName, string Section)>> GetAllLinksAsync() =>
        await db.QueryAsync(
            """
            SELECT ps."ParentUserId", s."Id" AS "StudentId", s."StudentId" AS "StudentCode", s."FirstName", s."LastName", g."Name" AS "GradeName", COALESCE(sec."Name", '') AS "SectionName"
            FROM "ParentStudents" ps
            JOIN "Students" s ON s."Id" = ps."StudentId"
            JOIN "Grades" g ON g."Id" = s."GradeId"
            LEFT JOIN "Sections" sec ON sec."Id" = s."SectionId"
            """,
            bind: null,
            reader => (
                ParentUserId: reader.GetInt32("ParentUserId"),
                StudentId: reader.GetInt32("StudentId"),
                StudentCode: reader.GetString("StudentCode"),
                FirstName: reader.GetString("FirstName"),
                LastName: reader.GetString("LastName"),
                GradeName: reader.GetString("GradeName"),
                Section: reader.GetString("SectionName")));
}
