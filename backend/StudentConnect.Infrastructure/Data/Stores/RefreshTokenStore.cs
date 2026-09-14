namespace StudentConnect.Infrastructure.Data.Stores;

// Long-lived, rotated-on-use tokens backing session renewal (see AuthController.Refresh) and
// real server-side logout (Revoke) — the JWT access token itself is never revocable.
public class RefreshTokenStore(Database db)
{
    public record TokenLookup(int Id, int UserId, DateTime ExpiresAtUtc, DateTime? RevokedAtUtc);

    public async Task<int> CreateAsync(int userId, string tokenHash, DateTime expiresAtUtc) =>
        await db.ExecuteScalarAsync<int>(
            """
            INSERT INTO "RefreshTokens" ("UserId", "TokenHash", "ExpiresAtUtc")
            VALUES (@userId, @tokenHash, @expiresAtUtc)
            RETURNING "Id"
            """,
            p =>
            {
                p.AddWithValue("userId", userId);
                p.AddWithValue("tokenHash", tokenHash);
                p.AddWithValue("expiresAtUtc", expiresAtUtc);
            });

    public async Task<TokenLookup?> GetByTokenHashAsync(string tokenHash) =>
        await db.QuerySingleOrDefaultAsync(
            """
            SELECT "Id", "UserId", "ExpiresAtUtc", "RevokedAtUtc" FROM "RefreshTokens"
            WHERE "TokenHash" = @tokenHash
            """,
            p => p.AddWithValue("tokenHash", tokenHash),
            reader => new TokenLookup(
                reader.GetInt32("Id"),
                reader.GetInt32("UserId"),
                reader.GetDateTimeUtc("ExpiresAtUtc"),
                reader.GetNullableDateTimeUtc("RevokedAtUtc")));

    public async Task RevokeAsync(int id) =>
        await db.ExecuteAsync(
            """UPDATE "RefreshTokens" SET "RevokedAtUtc" = now() WHERE "Id" = @id""",
            p => p.AddWithValue("id", id));

    public async Task RevokeByTokenHashAsync(string tokenHash) =>
        await db.ExecuteAsync(
            """UPDATE "RefreshTokens" SET "RevokedAtUtc" = now() WHERE "TokenHash" = @tokenHash AND "RevokedAtUtc" IS NULL""",
            p => p.AddWithValue("tokenHash", tokenHash));
}
