using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;

namespace StudentConnect.Infrastructure.Data.Stores;

public class TeacherSubjectAssignmentStore(Database db)
{
    private const string SelectWithNames = """
        SELECT tsa."Id", tsa."TeacherId", tsa."SubjectId", tsa."SectionId", tsa."CreatedAtUtc",
               t."FirstName" AS "TeacherFirstName", t."LastName" AS "TeacherLastName",
               sub."Name" AS "SubjectName", sec."Name" AS "SectionName", g."Name" AS "GradeName"
        FROM "TeacherSubjectAssignments" tsa
        JOIN "Teachers" t ON t."Id" = tsa."TeacherId"
        JOIN "Subjects" sub ON sub."Id" = tsa."SubjectId"
        JOIN "Sections" sec ON sec."Id" = tsa."SectionId"
        JOIN "Grades" g ON g."Id" = sec."GradeId"
        """;

    private static TeacherSubjectAssignment Map(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32("Id"),
        TeacherId = reader.GetInt32("TeacherId"),
        Teacher = new Teacher { FirstName = reader.GetString("TeacherFirstName"), LastName = reader.GetString("TeacherLastName") },
        SubjectId = reader.GetInt32("SubjectId"),
        Subject = new Subject { Name = reader.GetString("SubjectName") },
        SectionId = reader.GetInt32("SectionId"),
        Section = new Section { Name = reader.GetString("SectionName"), Grade = new Grade { Name = reader.GetString("GradeName") } },
        CreatedAtUtc = reader.GetDateTimeUtc("CreatedAtUtc"),
    };

    // Tenant-scoped transitively through Sections -> Grades -> SchoolId.
    public async Task<List<TeacherSubjectAssignment>> GetAllAsync(int schoolId) =>
        await db.QueryAsync(
            $"""{SelectWithNames} WHERE g."SchoolId" = @schoolId ORDER BY t."LastName", sub."Name", g."Name", sec."Name" """,
            p => p.AddWithValue("schoolId", schoolId),
            Map);

    public async Task<List<TeacherSubjectAssignment>> GetForTeacherAsync(int teacherId) =>
        await db.QueryAsync(
            $"""{SelectWithNames} WHERE tsa."TeacherId" = @teacherId ORDER BY sub."Name", g."Name", sec."Name" """,
            p => p.AddWithValue("teacherId", teacherId),
            Map);

    public async Task<TeacherSubjectAssignment?> GetByIdAsync(int id) =>
        await db.QuerySingleOrDefaultAsync($"""{SelectWithNames} WHERE tsa."Id" = @id""", p => p.AddWithValue("id", id), Map);

    public async Task<bool> ExistsAsync(int teacherId, int subjectId, int sectionId) =>
        await db.ExistsAsync(
            """
            SELECT 1 FROM "TeacherSubjectAssignments"
            WHERE "TeacherId" = @teacherId AND "SubjectId" = @subjectId AND "SectionId" = @sectionId
            """,
            p =>
            {
                p.AddWithValue("teacherId", teacherId);
                p.AddWithValue("subjectId", subjectId);
                p.AddWithValue("sectionId", sectionId);
            });

    public async Task<bool> ExistsAsync(int id, int schoolId) =>
        await db.ExistsAsync(
            """
            SELECT 1 FROM "TeacherSubjectAssignments" tsa
            JOIN "Sections" sec ON sec."Id" = tsa."SectionId"
            JOIN "Grades" g ON g."Id" = sec."GradeId"
            WHERE tsa."Id" = @id AND g."SchoolId" = @schoolId
            """,
            p =>
            {
                p.AddWithValue("id", id);
                p.AddWithValue("schoolId", schoolId);
            });

    public async Task<int> CreateAsync(int teacherId, int subjectId, int sectionId) =>
        await db.ExecuteScalarAsync<int>(
            """
            INSERT INTO "TeacherSubjectAssignments" ("TeacherId", "SubjectId", "SectionId")
            VALUES (@teacherId, @subjectId, @sectionId)
            RETURNING "Id"
            """,
            p =>
            {
                p.AddWithValue("teacherId", teacherId);
                p.AddWithValue("subjectId", subjectId);
                p.AddWithValue("sectionId", sectionId);
            });

    public async Task DeleteAsync(int id) =>
        await db.ExecuteAsync("""DELETE FROM "TeacherSubjectAssignments" WHERE "Id" = @id""", p => p.AddWithValue("id", id));
}
