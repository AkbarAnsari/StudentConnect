using StudentConnect.Application;

namespace StudentConnect.Infrastructure.Data.Stores;

// Long-lived, rotated-on-use tokens backing session renewal (see AuthController.Refresh) and
// real server-side logout (Revoke) — the JWT access token itself is never revocable.
public class RefreshTokenStore(Database db)
{
    public record TokenLookup(int Id, int UserId, Guid SessionId, DateTime ExpiresAtUtc, DateTime? RevokedAtUtc);

    public async Task<int> CreateAsync(int userId, string tokenHash, DateTime expiresAtUtc, Guid? sessionId = null, string? userAgent = null, string? ipAddress = null) =>
        await db.ExecuteScalarAsync<int>(
            """
            INSERT INTO "RefreshTokens" ("UserId", "TokenHash", "ExpiresAtUtc", "SessionId", "UserAgent", "IpAddress")
            VALUES (@userId, @tokenHash, @expiresAtUtc, @sessionId, @userAgent, @ipAddress)
            RETURNING "Id"
            """,
            p =>
            {
                p.AddWithValue("userId", userId);
                p.AddWithValue("tokenHash", tokenHash);
                p.AddWithValue("expiresAtUtc", expiresAtUtc);
                p.AddWithValue("sessionId", sessionId ?? Guid.NewGuid());
                p.AddWithValue("userAgent", (object?)userAgent ?? DBNull.Value);
                p.AddWithValue("ipAddress", (object?)ipAddress ?? DBNull.Value);
            });

    public async Task<TokenLookup?> GetByTokenHashAsync(string tokenHash) =>
        await db.QuerySingleOrDefaultAsync(
            """
            SELECT "Id", "UserId", "SessionId", "ExpiresAtUtc", "RevokedAtUtc" FROM "RefreshTokens"
            WHERE "TokenHash" = @tokenHash
            """,
            p => p.AddWithValue("tokenHash", tokenHash),
            reader => new TokenLookup(
                reader.GetInt32("Id"),
                reader.GetInt32("UserId"),
                reader.GetGuid(reader.GetOrdinal("SessionId")),
                reader.GetDateTimeUtc("ExpiresAtUtc"),
                reader.GetNullableDateTimeUtc("RevokedAtUtc")));

    public async Task<List<UserSessionDto>> GetActiveSessionsAsync(int userId) =>
        await db.QueryAsync(
            """
            SELECT "SessionId", MIN("CreatedAtUtc") AS "CreatedAtUtc", MAX("ExpiresAtUtc") AS "ExpiresAtUtc",
                   COALESCE(MAX("UserAgent"), 'Unknown device') AS "UserAgent", COALESCE(MAX("IpAddress"), 'Unknown') AS "IpAddress"
            FROM "RefreshTokens"
            WHERE "UserId" = @userId
            GROUP BY "SessionId"
            HAVING bool_or("RevokedAtUtc" IS NULL AND "ExpiresAtUtc" > now())
            ORDER BY MAX("CreatedAtUtc") DESC
            """,
            p => p.AddWithValue("userId", userId),
            reader => new UserSessionDto
            {
                SessionId = reader.GetGuid(reader.GetOrdinal("SessionId")),
                CreatedAtUtc = reader.GetDateTimeUtc("CreatedAtUtc"),
                ExpiresAtUtc = reader.GetDateTimeUtc("ExpiresAtUtc"),
                UserAgent = reader.GetString("UserAgent"),
                IpAddress = reader.GetString("IpAddress"),
            });

    public async Task<int> RevokeSessionAsync(int userId, Guid sessionId) =>
        await db.ExecuteAsync(
            """UPDATE "RefreshTokens" SET "RevokedAtUtc" = now() WHERE "UserId" = @userId AND "SessionId" = @sessionId AND "RevokedAtUtc" IS NULL""",
            p => { p.AddWithValue("userId", userId); p.AddWithValue("sessionId", sessionId); });

    public async Task RevokeAsync(int id) =>
        await db.ExecuteAsync(
            """UPDATE "RefreshTokens" SET "RevokedAtUtc" = now() WHERE "Id" = @id""",
            p => p.AddWithValue("id", id));

    public async Task RevokeByTokenHashAsync(string tokenHash) =>
        await db.ExecuteAsync(
            """UPDATE "RefreshTokens" SET "RevokedAtUtc" = now() WHERE "TokenHash" = @tokenHash AND "RevokedAtUtc" IS NULL""",
            p => p.AddWithValue("tokenHash", tokenHash));

    public async Task RevokeAllForUserAsync(int userId) =>
        await db.ExecuteAsync(
            """UPDATE "RefreshTokens" SET "RevokedAtUtc" = now() WHERE "UserId" = @userId AND "RevokedAtUtc" IS NULL""",
            p => p.AddWithValue("userId", userId));
}
