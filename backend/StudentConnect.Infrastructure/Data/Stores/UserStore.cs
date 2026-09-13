using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;

namespace StudentConnect.Infrastructure.Data.Stores;

public class UserStore(Database db)
{
    // Used at login — includes the Student/Teacher display-name fields the response needs,
    // without a full join into every column of those tables.
    public async Task<ApplicationUser?> GetByUsernameForLoginAsync(string username) =>
        await db.QuerySingleOrDefaultAsync(
            """
            SELECT u."Id", u."Username", u."PasswordHash", u."Role", u."StudentId", u."TeacherId", u."SchoolId",
                   st."FirstName" AS "StudentFirstName", st."LastName" AS "StudentLastName",
                   t."FirstName" AS "TeacherFirstName", t."LastName" AS "TeacherLastName"
            FROM "Users" u
            LEFT JOIN "Students" st ON st."Id" = u."StudentId"
            LEFT JOIN "Teachers" t ON t."Id" = u."TeacherId"
            WHERE u."Username" = @username
            """,
            p => p.AddWithValue("username", username),
            reader =>
            {
                var user = new ApplicationUser
                {
                    Id = reader.GetInt32("Id"),
                    Username = reader.GetString("Username"),
                    PasswordHash = reader.GetString("PasswordHash"),
                    Role = reader.GetEnum<UserRole>("Role"),
                    StudentId = reader.GetNullableInt32("StudentId"),
                    TeacherId = reader.GetNullableInt32("TeacherId"),
                    SchoolId = reader.GetNullableInt32("SchoolId") ?? 0,
                };
                if (user.StudentId is not null)
                {
                    user.Student = new Student { FirstName = reader.GetString("StudentFirstName"), LastName = reader.GetString("StudentLastName") };
                }
                if (user.TeacherId is not null)
                {
                    user.Teacher = new Teacher { FirstName = reader.GetString("TeacherFirstName"), LastName = reader.GetString("TeacherLastName") };
                }
                return user;
            });

    public async Task<bool> UsernameExistsAsync(string username) =>
        await db.ExistsAsync("""SELECT 1 FROM "Users" WHERE "Username" = @username""", p => p.AddWithValue("username", username));

    public async Task<int> CreateAsync(string username, string passwordHash, UserRole role, int schoolId, int? studentId = null, int? teacherId = null, string? email = null) =>
        await db.ExecuteScalarAsync<int>(
            """
            INSERT INTO "Users" ("Username", "PasswordHash", "Role", "StudentId", "TeacherId", "Email", "SchoolId")
            VALUES (@username, @passwordHash, @role, @studentId, @teacherId, @email, @schoolId)
            RETURNING "Id"
            """,
            p =>
            {
                p.AddWithValue("username", username);
                p.AddWithValue("passwordHash", passwordHash);
                p.AddWithValue("role", role.ToString());
                p.AddWithValue("studentId", (object?)studentId ?? DBNull.Value);
                p.AddWithValue("teacherId", (object?)teacherId ?? DBNull.Value);
                p.AddWithValue("email", (object?)email ?? DBNull.Value);
                p.AddWithValue("schoolId", schoolId);
            });

    // Resolves the best email address for a user: their own Email column (Parent accounts),
    // or their linked Student/Teacher record's Email otherwise. Null if none is available.
    public async Task<string?> GetEmailForUserAsync(int userId) =>
        await db.ExecuteScalarAsync<string>(
            """
            SELECT COALESCE(u."Email", st."Email", t."Email")
            FROM "Users" u
            LEFT JOIN "Students" st ON st."Id" = u."StudentId"
            LEFT JOIN "Teachers" t ON t."Id" = u."TeacherId"
            WHERE u."Id" = @userId
            """,
            p => p.AddWithValue("userId", userId));

    // Student-role user accounts linked to any of the given students — used to resolve
    // notification recipients (homework assigned to a whole grade, etc.).
    public async Task<List<int>> GetStudentUserIdsAsync(IReadOnlyCollection<int> studentIds)
    {
        if (studentIds.Count == 0)
        {
            return [];
        }

        return await db.QueryAsync(
            """SELECT "Id" FROM "Users" WHERE "StudentId" = ANY(@studentIds)""",
            p => p.AddWithValue("studentIds", studentIds.ToArray()),
            reader => reader.GetInt32("Id"));
    }

    public async Task<int?> GetUserIdForStudentAsync(int studentId) =>
        await db.ExecuteScalarAsync<int>("""SELECT "Id" FROM "Users" WHERE "StudentId" = @studentId""", p => p.AddWithValue("studentId", studentId));

    public async Task<List<ApplicationUser>> GetParentUsersAsync() =>
        await db.QueryAsync(
            """SELECT "Id", "Username", "Email" FROM "Users" WHERE "Role" = 'Parent' ORDER BY "Username" """,
            bind: null,
            reader => new ApplicationUser { Id = reader.GetInt32("Id"), Username = reader.GetString("Username"), Email = reader.GetNullableString("Email") });

    public async Task<bool> ParentExistsAsync(int userId) =>
        await db.ExistsAsync("""SELECT 1 FROM "Users" WHERE "Id" = @id AND "Role" = 'Parent'""", p => p.AddWithValue("id", userId));
}
