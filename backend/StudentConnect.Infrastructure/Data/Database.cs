using Npgsql;

namespace StudentConnect.Infrastructure.Data;

// Thin ADO.NET helper wrapping Npgsql directly — no ORM, no change tracking, no LINQ-to-SQL
// translation. Every Data/Stores/*.cs class builds on this. One connection per call, opened
// and disposed immediately (Npgsql pools physical connections internally, so this is cheap).
public class Database(IConfiguration configuration)
{
    private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:DefaultConnection.");

    public async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken ct = default)
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }

    public async Task<List<T>> QueryAsync<T>(string sql, Action<NpgsqlParameterCollection>? bind, Func<NpgsqlDataReader, T> map, CancellationToken ct = default)
    {
        await using var connection = await OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        bind?.Invoke(command.Parameters);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var results = new List<T>();
        while (await reader.ReadAsync(ct))
        {
            results.Add(map(reader));
        }
        return results;
    }

    public async Task<T?> QuerySingleOrDefaultAsync<T>(string sql, Action<NpgsqlParameterCollection>? bind, Func<NpgsqlDataReader, T> map, CancellationToken ct = default)
        where T : class
    {
        var results = await QueryAsync(sql, bind, map, ct);
        return results.Count > 0 ? results[0] : null;
    }

    public async Task<int> ExecuteAsync(string sql, Action<NpgsqlParameterCollection>? bind, CancellationToken ct = default)
    {
        await using var connection = await OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        bind?.Invoke(command.Parameters);
        return await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<T?> ExecuteScalarAsync<T>(string sql, Action<NpgsqlParameterCollection>? bind, CancellationToken ct = default)
    {
        await using var connection = await OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        bind?.Invoke(command.Parameters);
        var result = await command.ExecuteScalarAsync(ct);
        return result is null or DBNull ? default : (T)result;
    }

    public async Task<bool> ExistsAsync(string sql, Action<NpgsqlParameterCollection>? bind, CancellationToken ct = default)
    {
        await using var connection = await OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        bind?.Invoke(command.Parameters);
        var result = await command.ExecuteScalarAsync(ct);
        return result is not (null or DBNull);
    }

    // Runs multiple commands atomically against ONE connection/transaction — used where a
    // controller needs to read-then-write without a second request racing in between
    // (e.g. activity registration capacity checks).
    public async Task<TResult> WithTransactionAsync<TResult>(
        Func<NpgsqlConnection, NpgsqlTransaction, Task<TResult>> action,
        CancellationToken ct = default)
    {
        await using var connection = await OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        try
        {
            var result = await action(connection, transaction);
            await transaction.CommitAsync(ct);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}
