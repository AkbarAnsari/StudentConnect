using Npgsql;
using StudentConnect.Application;
using StudentConnect.Domain;

namespace StudentConnect.Infrastructure.Data.Stores;

public class UserStore(Database db)
{
    // Used at login — includes the Student/Teacher display-name fields the response needs,
    // without a full join into every column of those tables.
    private const string LoginSelect = """
         SELECT u."Id", u."Username", u."PasswordHash", u."Role", u."StudentId", u."TeacherId", u."SchoolId", u."Email", u."EmailVerifiedAtUtc",
             u."Status", u."CreatedAtUtc", u."LastLoginAtUtc", u."DeactivatedAtUtc",
               st."FirstName" AS "StudentFirstName", st."LastName" AS "StudentLastName",
               t."FirstName" AS "TeacherFirstName", t."LastName" AS "TeacherLastName"
        FROM "Users" u
        LEFT JOIN "Students" st ON st."Id" = u."StudentId"
        LEFT JOIN "Teachers" t ON t."Id" = u."TeacherId"
        """;

    private static ApplicationUser MapLogin(NpgsqlDataReader reader)
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
            EmailVerifiedAtUtc = reader.GetNullableDateTimeUtc("EmailVerifiedAtUtc"),
            Email = reader.GetNullableString("Email"),
            Status = reader.GetEnum<UserAccountStatus>("Status"),
            CreatedAtUtc = reader.GetDateTimeUtc("CreatedAtUtc"),
            LastLoginAtUtc = reader.GetNullableDateTimeUtc("LastLoginAtUtc"),
            DeactivatedAtUtc = reader.GetNullableDateTimeUtc("DeactivatedAtUtc"),
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
    }

    public async Task<ApplicationUser?> GetByUsernameForLoginAsync(string username) =>
        await db.QuerySingleOrDefaultAsync($"""{LoginSelect} WHERE u."Username" = @username""", p => p.AddWithValue("username", username), MapLogin);

    // Used to re-issue an access token from a refresh token, where all we have is the user id.
    public async Task<ApplicationUser?> GetByIdForLoginAsync(int id) =>
        await db.QuerySingleOrDefaultAsync($"""{LoginSelect} WHERE u."Id" = @id""", p => p.AddWithValue("id", id), MapLogin);

    public async Task MarkEmailVerifiedAsync(int userId) =>
        await db.ExecuteAsync(
            """UPDATE "Users" SET "EmailVerifiedAtUtc" = now() WHERE "Id" = @userId""",
            p => p.AddWithValue("userId", userId));

    public async Task MarkLoginAsync(int userId) =>
        await db.ExecuteAsync("""UPDATE "Users" SET "LastLoginAtUtc" = now() WHERE "Id" = @userId""", p => p.AddWithValue("userId", userId));

    public async Task<AccountProfileDto?> GetAccountAsync(int userId, int schoolId) =>
        await db.QuerySingleOrDefaultAsync(
            """
            SELECT u."Id", u."Username", u."Role", COALESCE(u."Email", st."Email", t."Email") AS "Email",
                   u."EmailVerifiedAtUtc", u."Status", u."CreatedAtUtc", u."LastLoginAtUtc",
                    CASE WHEN u."Role" = 'Admin' THEN 'Administrator'
                        ELSE COALESCE(NULLIF(concat_ws(' ', st."FirstName", st."LastName"), ''),
                                   NULLIF(concat_ws(' ', t."FirstName", t."LastName"), ''), u."Username") END AS "DisplayName"
            FROM "Users" u
            LEFT JOIN "Students" st ON st."Id" = u."StudentId"
            LEFT JOIN "Teachers" t ON t."Id" = u."TeacherId"
            WHERE u."Id" = @userId AND u."SchoolId" = @schoolId
            """,
            p => { p.AddWithValue("userId", userId); p.AddWithValue("schoolId", schoolId); },
            reader => new AccountProfileDto
            {
                Id = reader.GetInt32("Id"), Username = reader.GetString("Username"), Role = reader.GetString("Role"),
                DisplayName = reader.GetString("DisplayName"), Email = reader.GetNullableString("Email"),
                EmailVerified = reader.GetNullableDateTimeUtc("EmailVerifiedAtUtc") is not null,
                Status = reader.GetString("Status"), CreatedAtUtc = reader.GetDateTimeUtc("CreatedAtUtc"),
                LastLoginAtUtc = reader.GetNullableDateTimeUtc("LastLoginAtUtc"),
            });

    public async Task<List<UserAccessDto>> GetUsersForSchoolAsync(int schoolId) =>
        await db.QueryAsync(
            """
            SELECT u."Id", u."Username", u."Role", u."StudentId", u."TeacherId",
                   COALESCE(u."Email", st."Email", t."Email") AS "Email", u."EmailVerifiedAtUtc", u."Status",
                   u."CreatedAtUtc", u."LastLoginAtUtc", u."DeactivatedAtUtc",
                    CASE WHEN u."Role" = 'Admin' THEN 'Administrator'
                        ELSE COALESCE(NULLIF(concat_ws(' ', st."FirstName", st."LastName"), ''),
                                   NULLIF(concat_ws(' ', t."FirstName", t."LastName"), ''), u."Username") END AS "DisplayName"
            FROM "Users" u
            LEFT JOIN "Students" st ON st."Id" = u."StudentId"
            LEFT JOIN "Teachers" t ON t."Id" = u."TeacherId"
            WHERE u."SchoolId" = @schoolId
            ORDER BY u."Role", "DisplayName"
            """,
            p => p.AddWithValue("schoolId", schoolId),
            reader => new UserAccessDto
            {
                Id = reader.GetInt32("Id"), Username = reader.GetString("Username"), Role = reader.GetString("Role"),
                DisplayName = reader.GetString("DisplayName"), Email = reader.GetNullableString("Email"),
                EmailVerified = reader.GetNullableDateTimeUtc("EmailVerifiedAtUtc") is not null,
                Status = reader.GetString("Status"), CreatedAtUtc = reader.GetDateTimeUtc("CreatedAtUtc"),
                LastLoginAtUtc = reader.GetNullableDateTimeUtc("LastLoginAtUtc"),
                DeactivatedAtUtc = reader.GetNullableDateTimeUtc("DeactivatedAtUtc"),
                StudentId = reader.GetNullableInt32("StudentId"), TeacherId = reader.GetNullableInt32("TeacherId"),
            });

    public async Task UpdateEmailAsync(int userId, int schoolId, string? email) =>
        await db.ExecuteAsync(
            """UPDATE "Users" SET "Email" = @email, "EmailVerifiedAtUtc" = NULL WHERE "Id" = @userId AND "SchoolId" = @schoolId""",
            p => { p.AddWithValue("userId", userId); p.AddWithValue("schoolId", schoolId); p.AddWithValue("email", (object?)email ?? DBNull.Value); });

    public async Task<bool> SetStatusAsync(int userId, int schoolId, UserAccountStatus status) =>
        await db.ExecuteAsync(
            """UPDATE "Users" SET "Status" = @status, "DeactivatedAtUtc" = CASE WHEN @status = 'Deactivated' THEN now() ELSE NULL END WHERE "Id" = @userId AND "SchoolId" = @schoolId""",
            p => { p.AddWithValue("userId", userId); p.AddWithValue("schoolId", schoolId); p.AddWithValue("status", status.ToString()); }) > 0;

    public async Task<int> CountActiveAdminsAsync(int schoolId) =>
        (int)await db.ExecuteScalarAsync<long>("""SELECT COUNT(*) FROM "Users" WHERE "SchoolId" = @schoolId AND "Role" = 'Admin' AND "Status" = 'Active'""", p => p.AddWithValue("schoolId", schoolId));

    public async Task RevokeRefreshTokensAsync(int userId) =>
        await db.ExecuteAsync(
            """UPDATE "RefreshTokens" SET "RevokedAtUtc" = now() WHERE "UserId" = @userId AND "RevokedAtUtc" IS NULL""",
            p => p.AddWithValue("userId", userId));

    public async Task<bool> GetNotificationDigestEnabledAsync(int userId) =>
        await db.ExecuteScalarAsync<bool>(
            """SELECT "NotificationDigestEnabled" FROM "Users" WHERE "Id" = @userId""",
            p => p.AddWithValue("userId", userId));

    public async Task SetNotificationDigestPreferenceAsync(int userId, bool enabled) =>
        await db.ExecuteAsync(
            """UPDATE "Users" SET "NotificationDigestEnabled" = @enabled WHERE "Id" = @userId""",
            p =>
            {
                p.AddWithValue("userId", userId);
                p.AddWithValue("enabled", enabled);
            });

    public async Task<NotificationPreferenceDto> GetNotificationPreferencesAsync(int userId) =>
        await db.QuerySingleOrDefaultAsync(
            """SELECT "NotificationDigestEnabled", "EmailChannelEnabled", "InAppChannelEnabled", "QuietHoursStart", "QuietHoursEnd" FROM "Users" WHERE "Id" = @userId""",
            p => p.AddWithValue("userId", userId),
            reader => new NotificationPreferenceDto
            {
                DigestEnabled = reader.GetBoolean("NotificationDigestEnabled"),
                EmailChannelEnabled = reader.GetBoolean("EmailChannelEnabled"),
                InAppChannelEnabled = reader.GetBoolean("InAppChannelEnabled"),
                QuietHoursStart = reader.IsDBNull(reader.GetOrdinal("QuietHoursStart")) ? null : TimeOnly.FromTimeSpan(reader.GetTimeSpan(reader.GetOrdinal("QuietHoursStart"))),
                QuietHoursEnd = reader.IsDBNull(reader.GetOrdinal("QuietHoursEnd")) ? null : TimeOnly.FromTimeSpan(reader.GetTimeSpan(reader.GetOrdinal("QuietHoursEnd"))),
            }) ?? new NotificationPreferenceDto();

    public async Task SetNotificationPreferencesAsync(int userId, NotificationPreferenceDto dto) =>
        await db.ExecuteAsync(
            """UPDATE "Users" SET "NotificationDigestEnabled" = @digest, "EmailChannelEnabled" = @email, "InAppChannelEnabled" = @inApp, "QuietHoursStart" = @start, "QuietHoursEnd" = @end WHERE "Id" = @userId""",
            p => { p.AddWithValue("userId", userId); p.AddWithValue("digest", dto.DigestEnabled); p.AddWithValue("email", dto.EmailChannelEnabled); p.AddWithValue("inApp", dto.InAppChannelEnabled); p.AddWithValue("start", (object?)dto.QuietHoursStart?.ToTimeSpan() ?? DBNull.Value); p.AddWithValue("end", (object?)dto.QuietHoursEnd?.ToTimeSpan() ?? DBNull.Value); });

    public async Task<bool> UsernameExistsAsync(string username) =>
        await db.ExistsAsync("""SELECT 1 FROM "Users" WHERE "Username" = @username""", p => p.AddWithValue("username", username));

    public async Task<bool> IsActiveAsync(int userId, int schoolId) =>
        await db.ExistsAsync(
            """SELECT 1 FROM "Users" WHERE "Id" = @userId AND "SchoolId" = @schoolId AND "Status" = 'Active'""",
            p => { p.AddWithValue("userId", userId); p.AddWithValue("schoolId", schoolId); });

        public async Task<bool> CanLinkStudentAsync(int studentId, int schoolId) =>
                await db.ExistsAsync(
                        """
                        SELECT 1 FROM "Students" s
                        WHERE s."Id" = @studentId AND s."SchoolId" = @schoolId
                            AND NOT EXISTS (SELECT 1 FROM "Users" u WHERE u."StudentId" = s."Id")
                        """,
                        p => { p.AddWithValue("studentId", studentId); p.AddWithValue("schoolId", schoolId); });

        public async Task<bool> CanLinkTeacherAsync(int teacherId, int schoolId) =>
                await db.ExistsAsync(
                        """
                        SELECT 1 FROM "Teachers" t
                        WHERE t."Id" = @teacherId AND t."SchoolId" = @schoolId
                            AND NOT EXISTS (SELECT 1 FROM "Users" u WHERE u."TeacherId" = t."Id")
                        """,
                        p => { p.AddWithValue("teacherId", teacherId); p.AddWithValue("schoolId", schoolId); });

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

    public async Task UpdatePasswordHashAsync(int userId, string passwordHash) =>
        await db.ExecuteAsync(
            """UPDATE "Users" SET "PasswordHash" = @passwordHash WHERE "Id" = @userId""",
            p =>
            {
                p.AddWithValue("passwordHash", passwordHash);
                p.AddWithValue("userId", userId);
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

    public async Task<List<ApplicationUser>> GetParentUsersAsync(int schoolId) =>
        await db.QueryAsync(
            """SELECT "Id", "Username", "Email", "Status" FROM "Users" WHERE "Role" = 'Parent' AND "SchoolId" = @schoolId ORDER BY "Username" """,
            bind: p => p.AddWithValue("schoolId", schoolId),
            reader => new ApplicationUser { Id = reader.GetInt32("Id"), Username = reader.GetString("Username"), Email = reader.GetNullableString("Email"), Status = reader.GetEnum<UserAccountStatus>("Status") });

    public async Task<bool> ParentExistsAsync(int userId) =>
        await db.ExistsAsync("""SELECT 1 FROM "Users" WHERE "Id" = @id AND "Role" = 'Parent'""", p => p.AddWithValue("id", userId));

    public async Task<bool> ParentExistsAsync(int userId, int schoolId) =>
        await db.ExistsAsync(
            """SELECT 1 FROM "Users" WHERE "Id" = @id AND "Role" = 'Parent' AND "SchoolId" = @schoolId""",
            p =>
            {
                p.AddWithValue("id", userId);
                p.AddWithValue("schoolId", schoolId);
            });
}
