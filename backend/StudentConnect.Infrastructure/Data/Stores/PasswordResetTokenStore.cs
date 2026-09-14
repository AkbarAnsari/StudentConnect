namespace StudentConnect.Infrastructure.Data.Stores;

public class PasswordResetTokenStore(Database db)
{
    public record TokenLookup(int Id, int UserId);

    public async Task CreateAsync(int userId, string tokenHash, DateTime expiresAtUtc) =>
        await db.ExecuteAsync(
            """
            INSERT INTO "PasswordResetTokens" ("UserId", "TokenHash", "ExpiresAtUtc")
            VALUES (@userId, @tokenHash, @expiresAtUtc)
            """,
            p =>
            {
                p.AddWithValue("userId", userId);
                p.AddWithValue("tokenHash", tokenHash);
                p.AddWithValue("expiresAtUtc", expiresAtUtc);
            });

    // Null if the token doesn't exist, has expired, or has already been used.
    public async Task<TokenLookup?> GetValidByTokenHashAsync(string tokenHash) =>
        await db.QuerySingleOrDefaultAsync(
            """
            SELECT "Id", "UserId" FROM "PasswordResetTokens"
            WHERE "TokenHash" = @tokenHash AND "UsedAtUtc" IS NULL AND "ExpiresAtUtc" > now()
            """,
            p => p.AddWithValue("tokenHash", tokenHash),
            reader => new TokenLookup(reader.GetInt32("Id"), reader.GetInt32("UserId")));

    public async Task MarkUsedAsync(int id) =>
        await db.ExecuteAsync(
            """UPDATE "PasswordResetTokens" SET "UsedAtUtc" = now() WHERE "Id" = @id""",
            p => p.AddWithValue("id", id));
}
