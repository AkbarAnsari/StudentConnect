using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;

namespace StudentConnect.Infrastructure.Data.Stores;

public class StudentStore(Database db)
{
    private static Student MapWithGrade(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32("Id"),
        StudentId = reader.GetString("StudentId"),
        FirstName = reader.GetString("FirstName"),
        LastName = reader.GetString("LastName"),
        Email = reader.GetString("Email"),
        SchoolId = reader.GetNullableInt32("SchoolId") ?? 0,
        GradeId = reader.GetInt32("GradeId"),
        Grade = new Grade { Id = reader.GetInt32("GradeId"), Name = reader.GetString("GradeName") },
        SectionId = reader.GetNullableInt32("SectionId"),
        Section = reader.GetNullableInt32("SectionId") is { } sectionId
            ? new Section { Id = sectionId, Name = reader.GetNullableString("SectionName") ?? string.Empty }
            : null,
    };

    private const string SelectWithGrade = """
        SELECT s."Id", s."StudentId", s."FirstName", s."LastName", s."Email", s."SchoolId", s."GradeId", g."Name" AS "GradeName",
               s."SectionId", sec."Name" AS "SectionName"
        FROM "Students" s
        JOIN "Grades" g ON g."Id" = s."GradeId"
        LEFT JOIN "Sections" sec ON sec."Id" = s."SectionId"
        """;

    public async Task<List<Student>> GetAllAsync() =>
        await db.QueryAsync($"""{SelectWithGrade} ORDER BY s."Id" """, bind: null, map: MapWithGrade);

    // Tenant-scoped — used by the multi-tenancy-aware StudentsController.
    public async Task<List<Student>> GetAllAsync(int schoolId) =>
        await db.QueryAsync(
            $"""{SelectWithGrade} WHERE s."SchoolId" = @schoolId ORDER BY s."Id" """,
            p => p.AddWithValue("schoolId", schoolId),
            map: MapWithGrade);

    public async Task<Student?> GetByIdAsync(int id) =>
        await db.QuerySingleOrDefaultAsync(
            $"""{SelectWithGrade} WHERE s."Id" = @id""",
            bind: p => p.AddWithValue("id", id),
            map: MapWithGrade);

    public async Task<Student?> GetByIdAsync(int id, int schoolId) =>
        await db.QuerySingleOrDefaultAsync(
            $"""{SelectWithGrade} WHERE s."Id" = @id AND s."SchoolId" = @schoolId""",
            bind: p =>
            {
                p.AddWithValue("id", id);
                p.AddWithValue("schoolId", schoolId);
            },
            map: MapWithGrade);

    public async Task<bool> ExistsAsync(int id) =>
        await db.ExistsAsync("""SELECT 1 FROM "Students" WHERE "Id" = @id""", p => p.AddWithValue("id", id));

    public async Task<bool> ExistsAsync(int id, int schoolId) =>
        await db.ExistsAsync(
            """SELECT 1 FROM "Students" WHERE "Id" = @id AND "SchoolId" = @schoolId""",
            p =>
            {
                p.AddWithValue("id", id);
                p.AddWithValue("schoolId", schoolId);
            });

    public async Task<bool> StudentCodeExistsAsync(string studentCode) =>
        await db.ExistsAsync("""SELECT 1 FROM "Students" WHERE "StudentId" = @code""", p => p.AddWithValue("code", studentCode));

    public async Task<bool> StudentCodeExistsAsync(string studentCode, int schoolId) =>
        await db.ExistsAsync(
            """SELECT 1 FROM "Students" WHERE "StudentId" = @code AND "SchoolId" = @schoolId""",
            p =>
            {
                p.AddWithValue("code", studentCode);
                p.AddWithValue("schoolId", schoolId);
            });

    public async Task<int> CreateAsync(Student student)
    {
        var id = await db.ExecuteScalarAsync<int>(
            """
            INSERT INTO "Students" ("StudentId", "FirstName", "LastName", "Email", "GradeId", "SectionId", "SchoolId")
            VALUES (@studentId, @firstName, @lastName, @email, @gradeId, @sectionId, @schoolId)
            RETURNING "Id"
            """,
            p =>
            {
                p.AddWithValue("studentId", student.StudentId);
                p.AddWithValue("firstName", student.FirstName);
                p.AddWithValue("lastName", student.LastName);
                p.AddWithValue("email", student.Email);
                p.AddWithValue("gradeId", student.GradeId);
                p.AddWithValue("sectionId", (object?)student.SectionId ?? DBNull.Value);
                p.AddWithValue("schoolId", student.SchoolId);
            });
        return id;
    }

    public async Task UpdateAsync(Student student) =>
        await db.ExecuteAsync(
            """
            UPDATE "Students"
            SET "FirstName" = @firstName, "LastName" = @lastName, "Email" = @email, "GradeId" = @gradeId, "SectionId" = @sectionId
            WHERE "Id" = @id
            """,
            p =>
            {
                p.AddWithValue("id", student.Id);
                p.AddWithValue("firstName", student.FirstName);
                p.AddWithValue("lastName", student.LastName);
                p.AddWithValue("email", student.Email);
                p.AddWithValue("gradeId", student.GradeId);
                p.AddWithValue("sectionId", (object?)student.SectionId ?? DBNull.Value);
            });

    public async Task DeleteAsync(int id) =>
        await db.ExecuteAsync("""DELETE FROM "Students" WHERE "Id" = @id""", p => p.AddWithValue("id", id));

    public async Task<List<int>> GetSubjectIdsForStudentAsync(int studentId) =>
        await db.QueryAsync(
            """SELECT "SubjectId" FROM "StudentSubjects" WHERE "StudentId" = @studentId""",
            p => p.AddWithValue("studentId", studentId),
            reader => reader.GetInt32("SubjectId"));

    // Used by the homework "who submitted" teacher overview to build the class roster.
    public async Task<List<Student>> GetByGradeOrderedByFirstNameAsync(int gradeId) =>
        await db.QueryAsync(
            """SELECT "Id", "FirstName", "LastName" FROM "Students" WHERE "GradeId" = @gradeId ORDER BY "FirstName" """,
            p => p.AddWithValue("gradeId", gradeId),
            reader => new Student
            {
                Id = reader.GetInt32("Id"),
                FirstName = reader.GetString("FirstName"),
                LastName = reader.GetString("LastName"),
            });

    // Used to build a teacher's "My Classes" roster for a section they are homeroom teacher of.
    public async Task<List<Student>> GetBySectionAsync(int sectionId) =>
        await db.QueryAsync(
            """SELECT "Id", "StudentId", "FirstName", "LastName", "Email" FROM "Students" WHERE "SectionId" = @sectionId ORDER BY "FirstName" """,
            p => p.AddWithValue("sectionId", sectionId),
            reader => new Student
            {
                Id = reader.GetInt32("Id"),
                StudentId = reader.GetString("StudentId"),
                FirstName = reader.GetString("FirstName"),
                LastName = reader.GetString("LastName"),
                Email = reader.GetString("Email"),
            });
}
