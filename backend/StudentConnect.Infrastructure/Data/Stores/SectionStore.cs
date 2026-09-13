using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;

namespace StudentConnect.Infrastructure.Data.Stores;

public class SectionStore(Database db)
{
    private const string SelectWithGrade = """
        SELECT sec."Id", sec."GradeId", sec."Name", g."Name" AS "GradeName",
               sec."HomeroomTeacherId", t."FirstName" AS "TeacherFirstName", t."LastName" AS "TeacherLastName"
        FROM "Sections" sec
        JOIN "Grades" g ON g."Id" = sec."GradeId"
        LEFT JOIN "Teachers" t ON t."Id" = sec."HomeroomTeacherId"
        """;

    private static Section Map(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32("Id"),
        GradeId = reader.GetInt32("GradeId"),
        Grade = new Grade { Name = reader.GetString("GradeName") },
        Name = reader.GetString("Name"),
        HomeroomTeacherId = reader.GetNullableInt32("HomeroomTeacherId"),
        HomeroomTeacher = reader.GetNullableInt32("HomeroomTeacherId") is not null
            ? new Teacher { FirstName = reader.GetString("TeacherFirstName"), LastName = reader.GetString("TeacherLastName") }
            : null,
    };

    public async Task<List<Section>> GetAllAsync() =>
        await db.QueryAsync($"""{SelectWithGrade} ORDER BY g."Name", sec."Name" """, bind: null, map: Map);

    public async Task<Section?> GetByIdAsync(int id) =>
        await db.QuerySingleOrDefaultAsync(
            $"""{SelectWithGrade} WHERE sec."Id" = @id""",
            bind: p => p.AddWithValue("id", id),
            map: Map);

    public async Task<List<Section>> GetByGradeAsync(int gradeId) =>
        await db.QueryAsync(
            $"""{SelectWithGrade} WHERE sec."GradeId" = @gradeId ORDER BY sec."Name" """,
            p => p.AddWithValue("gradeId", gradeId),
            Map);

    // Used to build a teacher's "My Classes" roster — sections where they are the homeroom teacher.
    public async Task<List<Section>> GetForTeacherAsync(int teacherId) =>
        await db.QueryAsync(
            $"""{SelectWithGrade} WHERE sec."HomeroomTeacherId" = @teacherId ORDER BY g."Name", sec."Name" """,
            p => p.AddWithValue("teacherId", teacherId),
            Map);

    public async Task<bool> ExistsAsync(int id) =>
        await db.ExistsAsync("""SELECT 1 FROM "Sections" WHERE "Id" = @id""", p => p.AddWithValue("id", id));

    // Used to validate that a student's chosen SectionId actually belongs to their chosen Grade.
    public async Task<bool> BelongsToGradeAsync(int sectionId, int gradeId) =>
        await db.ExistsAsync(
            """SELECT 1 FROM "Sections" WHERE "Id" = @sectionId AND "GradeId" = @gradeId""",
            p =>
            {
                p.AddWithValue("sectionId", sectionId);
                p.AddWithValue("gradeId", gradeId);
            });

    public async Task<bool> NameExistsInGradeAsync(int gradeId, string name) =>
        await db.ExistsAsync(
            """SELECT 1 FROM "Sections" WHERE "GradeId" = @gradeId AND "Name" = @name""",
            p =>
            {
                p.AddWithValue("gradeId", gradeId);
                p.AddWithValue("name", name);
            });

    public async Task<int> CreateAsync(Section section) =>
        await db.ExecuteScalarAsync<int>(
            """INSERT INTO "Sections" ("GradeId", "Name", "HomeroomTeacherId") VALUES (@gradeId, @name, @homeroomTeacherId) RETURNING "Id" """,
            p =>
            {
                p.AddWithValue("gradeId", section.GradeId);
                p.AddWithValue("name", section.Name);
                p.AddWithValue("homeroomTeacherId", (object?)section.HomeroomTeacherId ?? DBNull.Value);
            });

    public async Task DeleteAsync(int id) =>
        await db.ExecuteAsync("""DELETE FROM "Sections" WHERE "Id" = @id""", p => p.AddWithValue("id", id));
}
