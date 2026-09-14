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
        Status = reader.GetEnum<StudentStatus>("Status"),
        StatusChangedAtUtc = reader.GetNullableDateTimeUtc("StatusChangedAtUtc"),
        StatusReason = reader.GetNullableString("StatusReason"),
        DateOfBirth = reader.GetNullableDateOnly("DateOfBirth"),
        Phone = reader.GetNullableString("Phone"),
        Address = reader.GetNullableString("Address"),
    };

    private const string SelectWithGrade = """
        SELECT s."Id", s."StudentId", s."FirstName", s."LastName", s."Email", s."SchoolId", s."GradeId", g."Name" AS "GradeName",
               s."SectionId", sec."Name" AS "SectionName", s."Status", s."StatusChangedAtUtc", s."StatusReason",
               s."DateOfBirth", s."Phone", s."Address"
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

    // Status-filtered — GET /api/students defaults to Active only; ?status=All removes the filter.
    public async Task<List<Student>> GetAllAsync(int schoolId, StudentStatus? statusFilter) =>
        await db.QueryAsync(
            statusFilter is null
                ? $"""{SelectWithGrade} WHERE s."SchoolId" = @schoolId ORDER BY s."Id" """
                : $"""{SelectWithGrade} WHERE s."SchoolId" = @schoolId AND s."Status" = @status ORDER BY s."Id" """,
            p =>
            {
                p.AddWithValue("schoolId", schoolId);
                if (statusFilter is not null)
                {
                    p.AddWithValue("status", statusFilter.Value.ToString());
                }
            },
            map: MapWithGrade);

    // Page/pageSize-bounded variant backing GET /api/students (see StudentsController) — keeps
    // the response fast and bounded regardless of how many students a school has.
    public async Task<(List<Student> Items, int TotalCount)> GetPagedAsync(int schoolId, StudentStatus? statusFilter, int page, int pageSize)
    {
        var whereClause = statusFilter is null
            ? """WHERE s."SchoolId" = @schoolId"""
            : """WHERE s."SchoolId" = @schoolId AND s."Status" = @status""";

        void Bind(NpgsqlParameterCollection p)
        {
            p.AddWithValue("schoolId", schoolId);
            if (statusFilter is not null)
            {
                p.AddWithValue("status", statusFilter.Value.ToString());
            }
        }

        var items = await db.QueryAsync(
            $"""{SelectWithGrade} {whereClause} ORDER BY s."Id" LIMIT @pageSize OFFSET @offset""",
            p =>
            {
                Bind(p);
                p.AddWithValue("pageSize", pageSize);
                p.AddWithValue("offset", (page - 1) * pageSize);
            },
            map: MapWithGrade);

        var totalCount = await db.ExecuteScalarAsync<long>(
            $"""SELECT COUNT(*) FROM "Students" s {whereClause}""",
            Bind);

        return (items, (int)totalCount);
    }

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
            INSERT INTO "Students" ("StudentId", "FirstName", "LastName", "Email", "GradeId", "SectionId", "SchoolId", "DateOfBirth", "Phone", "Address")
            VALUES (@studentId, @firstName, @lastName, @email, @gradeId, @sectionId, @schoolId, @dateOfBirth, @phone, @address)
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
                p.AddWithValue("dateOfBirth", (object?)student.DateOfBirth ?? DBNull.Value);
                p.AddWithValue("phone", (object?)student.Phone ?? DBNull.Value);
                p.AddWithValue("address", (object?)student.Address ?? DBNull.Value);
            });
        return id;
    }

    public async Task UpdateAsync(Student student) =>
        await db.ExecuteAsync(
            """
            UPDATE "Students"
            SET "FirstName" = @firstName, "LastName" = @lastName, "Email" = @email, "GradeId" = @gradeId, "SectionId" = @sectionId,
                "DateOfBirth" = @dateOfBirth, "Phone" = @phone, "Address" = @address
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
                p.AddWithValue("dateOfBirth", (object?)student.DateOfBirth ?? DBNull.Value);
                p.AddWithValue("phone", (object?)student.Phone ?? DBNull.Value);
                p.AddWithValue("address", (object?)student.Address ?? DBNull.Value);
            });

    // A student's real lifecycle change (withdraw/graduate/transfer/reactivate) — never a
    // delete, so attendance/marks/homework history stays intact and queryable.
    public async Task UpdateStatusAsync(int id, StudentStatus status, string? reason) =>
        await db.ExecuteAsync(
            """UPDATE "Students" SET "Status" = @status, "StatusChangedAtUtc" = now(), "StatusReason" = @reason WHERE "Id" = @id""",
            p =>
            {
                p.AddWithValue("id", id);
                p.AddWithValue("status", status.ToString());
                p.AddWithValue("reason", (object?)reason ?? DBNull.Value);
            });

    // Used inside the academic-year rollover transaction (see AcademicYearsController) —
    // one connection/transaction shared across every promoted student's update.
    public async Task UpdateGradeSectionInTxAsync(NpgsqlConnection connection, NpgsqlTransaction tx, int studentId, int gradeId, int? sectionId)
    {
        await using var command = new NpgsqlCommand(
            """UPDATE "Students" SET "GradeId" = @gradeId, "SectionId" = @sectionId WHERE "Id" = @studentId""",
            connection,
            tx);
        command.Parameters.AddWithValue("gradeId", gradeId);
        command.Parameters.AddWithValue("sectionId", (object?)sectionId ?? DBNull.Value);
        command.Parameters.AddWithValue("studentId", studentId);
        await command.ExecuteNonQueryAsync();
    }

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
