using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;

namespace StudentConnect.Infrastructure.Data.Stores;

public sealed record StudentLoginInvitation(
    string Username,
    string PasswordHash,
    string TokenHash,
    DateTime ExpiresAtUtc,
    string EmailSubject,
    string EmailBody);

public sealed record StudentEnrollmentResult(int StudentId, int? UserId);
public sealed record StudentGuardianLink(int ParentUserId, string RelationshipType, bool IsPrimaryContact, int? ChangedByUserId);

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
    public async Task<(List<Student> Items, int TotalCount)> GetPagedAsync(
        int schoolId,
        StudentStatus? statusFilter,
        int page,
        int pageSize,
        int? gradeId = null,
        int? sectionId = null,
        string? query = null,
        string? sort = null)
    {
        var conditions = new List<string> { """s."SchoolId" = @schoolId""" };
        if (statusFilter is not null)
        {
            conditions.Add("""s."Status" = @status""");
        }
        if (gradeId is not null)
        {
            conditions.Add("""s."GradeId" = @gradeId""");
        }
        if (sectionId is not null)
        {
            conditions.Add("""s."SectionId" = @sectionId""");
        }
        if (!string.IsNullOrWhiteSpace(query))
        {
            conditions.Add(
                """(s."StudentId" ILIKE @query ESCAPE '\' OR s."FirstName" ILIKE @query ESCAPE '\' OR s."LastName" ILIKE @query ESCAPE '\' OR concat_ws(' ', s."FirstName", s."LastName") ILIKE @query ESCAPE '\')""");
        }
        var whereClause = "WHERE " + string.Join(" AND ", conditions);
        var orderBy = sort?.Trim().ToLowerInvariant() switch
        {
            "name-desc" => """s."LastName" DESC, s."FirstName" DESC, s."Id" DESC""",
            "student-id" => """s."StudentId" ASC, s."Id" ASC""",
            "student-id-desc" => """s."StudentId" DESC, s."Id" DESC""",
            "grade" => """g."Name" ASC, sec."Name" ASC NULLS LAST, s."LastName" ASC, s."FirstName" ASC""",
            "grade-desc" => """g."Name" DESC, sec."Name" DESC NULLS LAST, s."LastName" ASC, s."FirstName" ASC""",
            _ => """s."LastName" ASC, s."FirstName" ASC, s."Id" ASC""",
        };

        void Bind(NpgsqlParameterCollection p)
        {
            p.AddWithValue("schoolId", schoolId);
            if (statusFilter is not null)
            {
                p.AddWithValue("status", statusFilter.Value.ToString());
            }
            if (gradeId is not null)
            {
                p.AddWithValue("gradeId", gradeId.Value);
            }
            if (sectionId is not null)
            {
                p.AddWithValue("sectionId", sectionId.Value);
            }
            if (!string.IsNullOrWhiteSpace(query))
            {
                var escapedQuery = query.Trim()
                    .Replace("\\", "\\\\", StringComparison.Ordinal)
                    .Replace("%", "\\%", StringComparison.Ordinal)
                    .Replace("_", "\\_", StringComparison.Ordinal);
                p.AddWithValue("query", $"%{escapedQuery}%");
            }
        }

        var items = await db.QueryAsync(
            $"""{SelectWithGrade} {whereClause} ORDER BY {orderBy} LIMIT @pageSize OFFSET @offset""",
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

    public async Task<int> CountByIdsAsync(IReadOnlyCollection<int> ids, int schoolId) =>
        ids.Count == 0 ? 0 : (int)await db.ExecuteScalarAsync<long>(
            """SELECT COUNT(*) FROM "Students" WHERE "Id" = ANY(@ids) AND "SchoolId" = @schoolId""",
            p => { p.AddWithValue("ids", ids.Distinct().ToArray()); p.AddWithValue("schoolId", schoolId); });

    public async Task<int> UpdateStatusAsync(IReadOnlyCollection<int> ids, int schoolId, StudentStatus status, string? reason) =>
        ids.Count == 0 ? 0 : await db.ExecuteAsync(
            """
            UPDATE "Students" SET "Status" = @status, "StatusChangedAtUtc" = now(), "StatusReason" = @reason
            WHERE "Id" = ANY(@ids) AND "SchoolId" = @schoolId AND "Status" <> @status
            """,
            p =>
            {
                p.AddWithValue("ids", ids.Distinct().ToArray());
                p.AddWithValue("schoolId", schoolId);
                p.AddWithValue("status", status.ToString());
                p.AddWithValue("reason", (object?)reason ?? DBNull.Value);
            });

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

    public async Task<StudentEnrollmentResult> CreateEnrollmentAsync(
        Student student,
        IReadOnlyCollection<int> subjectIds,
        StudentLoginInvitation? invitation,
        StudentGuardianLink? guardian = null) =>
        await db.WithTransactionAsync(async (connection, transaction) =>
        {
            await using var studentCommand = new NpgsqlCommand(
                """
                INSERT INTO "Students" ("StudentId", "FirstName", "LastName", "Email", "GradeId", "SectionId", "SchoolId", "DateOfBirth", "Phone", "Address")
                VALUES (@studentId, @firstName, @lastName, @email, @gradeId, @sectionId, @schoolId, @dateOfBirth, @phone, @address)
                RETURNING "Id"
                """,
                connection,
                transaction);
            studentCommand.Parameters.AddWithValue("studentId", student.StudentId);
            studentCommand.Parameters.AddWithValue("firstName", student.FirstName);
            studentCommand.Parameters.AddWithValue("lastName", student.LastName);
            studentCommand.Parameters.AddWithValue("email", student.Email);
            studentCommand.Parameters.AddWithValue("gradeId", student.GradeId);
            studentCommand.Parameters.AddWithValue("sectionId", (object?)student.SectionId ?? DBNull.Value);
            studentCommand.Parameters.AddWithValue("schoolId", student.SchoolId);
            studentCommand.Parameters.AddWithValue("dateOfBirth", (object?)student.DateOfBirth ?? DBNull.Value);
            studentCommand.Parameters.AddWithValue("phone", (object?)student.Phone ?? DBNull.Value);
            studentCommand.Parameters.AddWithValue("address", (object?)student.Address ?? DBNull.Value);
            var studentId = Convert.ToInt32(await studentCommand.ExecuteScalarAsync());

            await using var subjectsCommand = new NpgsqlCommand(
                """
                INSERT INTO "StudentSubjects" ("StudentId", "SubjectId")
                SELECT @studentId, unnest(@subjectIds)
                """,
                connection,
                transaction);
            subjectsCommand.Parameters.AddWithValue("studentId", studentId);
            subjectsCommand.Parameters.AddWithValue("subjectIds", subjectIds.Distinct().ToArray());
            await subjectsCommand.ExecuteNonQueryAsync();

            if (guardian is not null)
            {
                if (guardian.IsPrimaryContact)
                {
                    await using var clearPrimary = new NpgsqlCommand(
                        """UPDATE "ParentStudents" SET "IsPrimaryContact" = false WHERE "StudentId" = @studentId""",
                        connection,
                        transaction);
                    clearPrimary.Parameters.AddWithValue("studentId", studentId);
                    await clearPrimary.ExecuteNonQueryAsync();
                }

                await using var guardianCommand = new NpgsqlCommand(
                    """INSERT INTO "ParentStudents" ("ParentUserId", "StudentId", "RelationshipType", "IsPrimaryContact", "CreatedByUserId") VALUES (@parentUserId, @studentId, @relationshipType, @isPrimaryContact, @changedByUserId)""",
                    connection,
                    transaction);
                guardianCommand.Parameters.AddWithValue("parentUserId", guardian.ParentUserId);
                guardianCommand.Parameters.AddWithValue("studentId", studentId);
                guardianCommand.Parameters.AddWithValue("relationshipType", guardian.RelationshipType);
                guardianCommand.Parameters.AddWithValue("isPrimaryContact", guardian.IsPrimaryContact);
                guardianCommand.Parameters.AddWithValue("changedByUserId", (object?)guardian.ChangedByUserId ?? DBNull.Value);
                await guardianCommand.ExecuteNonQueryAsync();

                await using var historyCommand = new NpgsqlCommand(
                    """INSERT INTO "ParentStudentHistory" ("ParentUserId", "StudentId", "Action", "RelationshipType", "IsPrimaryContact", "Reason", "ChangedByUserId") VALUES (@parentUserId, @studentId, 'Linked', @relationshipType, @isPrimaryContact, 'Linked during student enrollment', @changedByUserId)""",
                    connection,
                    transaction);
                historyCommand.Parameters.AddWithValue("parentUserId", guardian.ParentUserId);
                historyCommand.Parameters.AddWithValue("studentId", studentId);
                historyCommand.Parameters.AddWithValue("relationshipType", guardian.RelationshipType);
                historyCommand.Parameters.AddWithValue("isPrimaryContact", guardian.IsPrimaryContact);
                historyCommand.Parameters.AddWithValue("changedByUserId", (object?)guardian.ChangedByUserId ?? DBNull.Value);
                await historyCommand.ExecuteNonQueryAsync();
            }

            if (invitation is null)
            {
                return new StudentEnrollmentResult(studentId, null);
            }

            await using var userCommand = new NpgsqlCommand(
                """
                INSERT INTO "Users" ("Username", "PasswordHash", "Role", "StudentId", "Email", "SchoolId")
                VALUES (@username, @passwordHash, 'Student', @studentId, @email, @schoolId)
                RETURNING "Id"
                """,
                connection,
                transaction);
            userCommand.Parameters.AddWithValue("username", invitation.Username);
            userCommand.Parameters.AddWithValue("passwordHash", invitation.PasswordHash);
            userCommand.Parameters.AddWithValue("studentId", studentId);
            userCommand.Parameters.AddWithValue("email", student.Email);
            userCommand.Parameters.AddWithValue("schoolId", student.SchoolId);
            var userId = Convert.ToInt32(await userCommand.ExecuteScalarAsync());

            await using var tokenCommand = new NpgsqlCommand(
                """
                INSERT INTO "PasswordResetTokens" ("UserId", "TokenHash", "ExpiresAtUtc")
                VALUES (@userId, @tokenHash, @expiresAtUtc)
                """,
                connection,
                transaction);
            tokenCommand.Parameters.AddWithValue("userId", userId);
            tokenCommand.Parameters.AddWithValue("tokenHash", invitation.TokenHash);
            tokenCommand.Parameters.AddWithValue("expiresAtUtc", invitation.ExpiresAtUtc);
            await tokenCommand.ExecuteNonQueryAsync();

            await using var outboxCommand = new NpgsqlCommand(
                """
                INSERT INTO "EmailOutbox" ("ToEmail", "ToUserId", "Subject", "Body")
                VALUES (@email, @userId, @subject, @body)
                """,
                connection,
                transaction);
            outboxCommand.Parameters.AddWithValue("email", student.Email);
            outboxCommand.Parameters.AddWithValue("userId", userId);
            outboxCommand.Parameters.AddWithValue("subject", invitation.EmailSubject);
            outboxCommand.Parameters.AddWithValue("body", invitation.EmailBody);
            await outboxCommand.ExecuteNonQueryAsync();

            return new StudentEnrollmentResult(studentId, userId);
        });

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

    // Used to check teacher-subject-assignment authorization on Attendance/Marks bulk saves —
    // resolves each student's SectionId so the caller can require an assignment per section touched.
    public async Task<Dictionary<int, int?>> GetSectionIdsAsync(IReadOnlyCollection<int> studentIds)
    {
        if (studentIds.Count == 0)
        {
            return [];
        }

        var rows = await db.QueryAsync(
            """SELECT "Id", "SectionId" FROM "Students" WHERE "Id" = ANY(@studentIds)""",
            p => p.AddWithValue("studentIds", studentIds.ToArray()),
            reader => (Id: reader.GetInt32("Id"), SectionId: reader.GetNullableInt32("SectionId")));

        return rows.ToDictionary(r => r.Id, r => r.SectionId);
    }

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

    public async Task<List<Student>> GetActiveByGradeAsync(int gradeId, int schoolId) =>
        await db.QueryAsync(
            """
            SELECT "Id", "FirstName", "LastName"
            FROM "Students"
            WHERE "GradeId" = @gradeId AND "SchoolId" = @schoolId AND "Status" = 'Active'
            ORDER BY "FirstName", "LastName"
            """,
            p =>
            {
                p.AddWithValue("gradeId", gradeId);
                p.AddWithValue("schoolId", schoolId);
            },
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

    public async Task<List<Student>> GetActiveBySectionAsync(int sectionId, int schoolId) =>
        await db.QueryAsync(
            """
            SELECT "Id", "StudentId", "FirstName", "LastName", "Email"
            FROM "Students"
            WHERE "SectionId" = @sectionId AND "SchoolId" = @schoolId AND "Status" = 'Active'
            ORDER BY "FirstName", "LastName"
            """,
            p =>
            {
                p.AddWithValue("sectionId", sectionId);
                p.AddWithValue("schoolId", schoolId);
            },
            reader => new Student
            {
                Id = reader.GetInt32("Id"),
                StudentId = reader.GetString("StudentId"),
                FirstName = reader.GetString("FirstName"),
                LastName = reader.GetString("LastName"),
                Email = reader.GetString("Email"),
            });

    public async Task<List<Student>> GetActiveBySectionAndSubjectAsync(int sectionId, int subjectId, int schoolId) =>
        await db.QueryAsync(
            """
            SELECT s."Id", s."StudentId", s."FirstName", s."LastName", s."Email"
            FROM "Students" s
            JOIN "StudentSubjects" ss ON ss."StudentId" = s."Id"
            WHERE s."SectionId" = @sectionId
              AND ss."SubjectId" = @subjectId
              AND s."SchoolId" = @schoolId
              AND s."Status" = 'Active'
            ORDER BY s."FirstName", s."LastName"
            """,
            p =>
            {
                p.AddWithValue("sectionId", sectionId);
                p.AddWithValue("subjectId", subjectId);
                p.AddWithValue("schoolId", schoolId);
            },
            reader => new Student
            {
                Id = reader.GetInt32("Id"),
                StudentId = reader.GetString("StudentId"),
                FirstName = reader.GetString("FirstName"),
                LastName = reader.GetString("LastName"),
                Email = reader.GetString("Email"),
            });

    public async Task<List<Student>> GetActiveByGradeAndSubjectAsync(int gradeId, int subjectId, int schoolId) =>
        await db.QueryAsync(
            """
            SELECT s."Id", s."StudentId", s."FirstName", s."LastName", s."Email"
            FROM "Students" s
            JOIN "StudentSubjects" ss ON ss."StudentId" = s."Id"
            WHERE s."GradeId" = @gradeId
              AND ss."SubjectId" = @subjectId
              AND s."SchoolId" = @schoolId
              AND s."Status" = 'Active'
            ORDER BY s."FirstName", s."LastName"
            """,
            p =>
            {
                p.AddWithValue("gradeId", gradeId);
                p.AddWithValue("subjectId", subjectId);
                p.AddWithValue("schoolId", schoolId);
            },
            reader => new Student
            {
                Id = reader.GetInt32("Id"),
                StudentId = reader.GetString("StudentId"),
                FirstName = reader.GetString("FirstName"),
                LastName = reader.GetString("LastName"),
                Email = reader.GetString("Email"),
            });

    public async Task<bool> AreActiveStudentsEnrolledInClassAsync(
        IReadOnlyCollection<int> studentIds,
        int sectionId,
        int subjectId,
        int schoolId)
    {
        if (studentIds.Count == 0)
        {
            return false;
        }

        var distinctIds = studentIds.Distinct().ToArray();
        var matchingCount = await db.ExecuteScalarAsync<long>(
            """
            SELECT COUNT(DISTINCT s."Id")
            FROM "Students" s
            JOIN "StudentSubjects" ss ON ss."StudentId" = s."Id"
            WHERE s."Id" = ANY(@studentIds)
              AND s."SectionId" = @sectionId
              AND ss."SubjectId" = @subjectId
              AND s."SchoolId" = @schoolId
              AND s."Status" = 'Active'
            """,
            p =>
            {
                p.AddWithValue("studentIds", distinctIds);
                p.AddWithValue("sectionId", sectionId);
                p.AddWithValue("subjectId", subjectId);
                p.AddWithValue("schoolId", schoolId);
            });

        return matchingCount == distinctIds.Length;
    }
}
