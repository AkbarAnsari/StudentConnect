using StudentConnect.Application;
using StudentConnect.Domain;

namespace StudentConnect.Infrastructure.Data.Stores;

public class ParentStore(Database db)
{
    public async Task<List<ParentChildDto>> GetChildrenForParentAsync(int parentUserId) =>
        await db.QueryAsync(
            """
                 SELECT s."Id", s."StudentId", s."FirstName", s."LastName", g."Name" AS "GradeName", COALESCE(sec."Name", '') AS "SectionName",
                     ps."RelationshipType", ps."IsPrimaryContact"
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
                RelationshipType = reader.GetString("RelationshipType"),
                IsPrimaryContact = reader.GetBoolean("IsPrimaryContact"),
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

    public async Task LinkChildAsync(int parentUserId, int studentId, int? changedByUserId = null,
        string relationshipType = "Guardian", bool isPrimaryContact = false, string? reason = null) =>
        await db.WithTransactionAsync(async (connection, transaction) =>
        {
            if (isPrimaryContact)
            {
                await using var clearPrimary = new Npgsql.NpgsqlCommand(
                    """UPDATE "ParentStudents" SET "IsPrimaryContact" = false WHERE "StudentId" = @studentId""", connection, transaction);
                clearPrimary.Parameters.AddWithValue("studentId", studentId);
                await clearPrimary.ExecuteNonQueryAsync();
            }

            await using var link = new Npgsql.NpgsqlCommand(
                """INSERT INTO "ParentStudents" ("ParentUserId", "StudentId", "RelationshipType", "IsPrimaryContact", "CreatedByUserId") VALUES (@parentUserId, @studentId, @relationshipType, @isPrimaryContact, @changedByUserId)""",
                connection,
                transaction);
            BindHistory(link.Parameters, parentUserId, studentId, relationshipType, isPrimaryContact, changedByUserId, reason);
            await link.ExecuteNonQueryAsync();

            await InsertHistoryAsync(connection, transaction, parentUserId, studentId, "Linked", relationshipType, isPrimaryContact, changedByUserId, reason);
            return 0;
        });

    public async Task<int> UnlinkChildAsync(int parentUserId, int studentId, int? changedByUserId = null, string? reason = null) =>
        await db.WithTransactionAsync(async (connection, transaction) =>
        {
            await using var lookup = new Npgsql.NpgsqlCommand(
                """SELECT "RelationshipType", "IsPrimaryContact" FROM "ParentStudents" WHERE "ParentUserId" = @parentUserId AND "StudentId" = @studentId FOR UPDATE""",
                connection,
                transaction);
            lookup.Parameters.AddWithValue("parentUserId", parentUserId);
            lookup.Parameters.AddWithValue("studentId", studentId);
            string? relationshipType = null;
            var isPrimaryContact = false;
            await using (var reader = await lookup.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    relationshipType = reader.GetString(0);
                    isPrimaryContact = reader.GetBoolean(1);
                }
            }
            if (relationshipType is null) return 0;

            await using var unlink = new Npgsql.NpgsqlCommand(
                """DELETE FROM "ParentStudents" WHERE "ParentUserId" = @parentUserId AND "StudentId" = @studentId""",
                connection,
                transaction);
            unlink.Parameters.AddWithValue("parentUserId", parentUserId);
            unlink.Parameters.AddWithValue("studentId", studentId);
            var deleted = await unlink.ExecuteNonQueryAsync();
            await InsertHistoryAsync(connection, transaction, parentUserId, studentId, "Unlinked", relationshipType, isPrimaryContact, changedByUserId, reason);
            return deleted;
        });

    public async Task<List<ParentStudentHistoryDto>> GetHistoryAsync(int parentUserId) =>
        await db.QueryAsync(
            """
            SELECT h."Id", h."StudentId", h."Action", h."RelationshipType", h."IsPrimaryContact", h."Reason", h."ChangedAtUtc",
                   COALESCE(concat_ws(' ', s."FirstName", s."LastName"), 'Student ' || h."StudentId") AS "StudentName",
                   COALESCE(NULLIF(concat_ws(' ', st."FirstName", st."LastName"), ''), NULLIF(concat_ws(' ', t."FirstName", t."LastName"), ''), u."Username", 'System') AS "ChangedByName"
            FROM "ParentStudentHistory" h
            LEFT JOIN "Students" s ON s."Id" = h."StudentId"
            LEFT JOIN "Users" u ON u."Id" = h."ChangedByUserId"
            LEFT JOIN "Students" st ON st."Id" = u."StudentId"
            LEFT JOIN "Teachers" t ON t."Id" = u."TeacherId"
            WHERE h."ParentUserId" = @parentUserId
            ORDER BY h."ChangedAtUtc" DESC
            """,
            p => p.AddWithValue("parentUserId", parentUserId),
            reader => new ParentStudentHistoryDto
            {
                Id = reader.GetInt64("Id"), StudentId = reader.GetInt32("StudentId"), StudentName = reader.GetString("StudentName"),
                Action = reader.GetString("Action"), RelationshipType = reader.GetString("RelationshipType"),
                IsPrimaryContact = reader.GetBoolean("IsPrimaryContact"), Reason = reader.GetNullableString("Reason"),
                ChangedByName = reader.GetString("ChangedByName"), ChangedAtUtc = reader.GetDateTimeUtc("ChangedAtUtc"),
            });

    // Every parent user with their linked children — used by the admin "manage parents" list.
    public async Task<List<(int ParentUserId, int StudentId, string StudentCode, string FirstName, string LastName, string GradeName, string Section, string RelationshipType, bool IsPrimaryContact)>> GetAllLinksAsync(int schoolId) =>
        await db.QueryAsync(
            """
            SELECT ps."ParentUserId", s."Id" AS "StudentId", s."StudentId" AS "StudentCode", s."FirstName", s."LastName", g."Name" AS "GradeName", COALESCE(sec."Name", '') AS "SectionName", ps."RelationshipType", ps."IsPrimaryContact"
            FROM "ParentStudents" ps
            JOIN "Students" s ON s."Id" = ps."StudentId"
            JOIN "Grades" g ON g."Id" = s."GradeId"
            LEFT JOIN "Sections" sec ON sec."Id" = s."SectionId"
            WHERE s."SchoolId" = @schoolId
            """,
            bind: p => p.AddWithValue("schoolId", schoolId),
            reader => (
                ParentUserId: reader.GetInt32("ParentUserId"),
                StudentId: reader.GetInt32("StudentId"),
                StudentCode: reader.GetString("StudentCode"),
                FirstName: reader.GetString("FirstName"),
                LastName: reader.GetString("LastName"),
                GradeName: reader.GetString("GradeName"),
                Section: reader.GetString("SectionName"),
                RelationshipType: reader.GetString("RelationshipType"),
                IsPrimaryContact: reader.GetBoolean("IsPrimaryContact")));

    private static void BindHistory(Npgsql.NpgsqlParameterCollection parameters, int parentUserId, int studentId,
        string relationshipType, bool isPrimaryContact, int? changedByUserId, string? reason)
    {
        parameters.AddWithValue("parentUserId", parentUserId);
        parameters.AddWithValue("studentId", studentId);
        parameters.AddWithValue("relationshipType", relationshipType);
        parameters.AddWithValue("isPrimaryContact", isPrimaryContact);
        parameters.AddWithValue("changedByUserId", (object?)changedByUserId ?? DBNull.Value);
        parameters.AddWithValue("reason", (object?)reason ?? DBNull.Value);
    }

    private static async Task InsertHistoryAsync(Npgsql.NpgsqlConnection connection, Npgsql.NpgsqlTransaction transaction,
        int parentUserId, int studentId, string action, string relationshipType, bool isPrimaryContact, int? changedByUserId, string? reason)
    {
        await using var history = new Npgsql.NpgsqlCommand(
            """INSERT INTO "ParentStudentHistory" ("ParentUserId", "StudentId", "Action", "RelationshipType", "IsPrimaryContact", "Reason", "ChangedByUserId") VALUES (@parentUserId, @studentId, @action, @relationshipType, @isPrimaryContact, @reason, @changedByUserId)""",
            connection,
            transaction);
        BindHistory(history.Parameters, parentUserId, studentId, relationshipType, isPrimaryContact, changedByUserId, reason);
        history.Parameters.AddWithValue("action", action);
        await history.ExecuteNonQueryAsync();
    }
}
