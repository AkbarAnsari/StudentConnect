using Microsoft.Extensions.Configuration;
using StudentConnect.Infrastructure.Data;
using Xunit;

namespace StudentConnect.Api.Tests;

// Applies database/schema.sql + database/seed.sql once against a real Postgres test database
// (there's no more EF Core InMemory provider to fall back on now that the API talks to
// Postgres directly via ADO.NET). Point STUDENTCONNECT_TEST_CONNECTION_STRING at a disposable
// database if the default local one isn't suitable — never runs against the dev database.
public class DatabaseFixture : IAsyncLifetime
{
    private const string DefaultTestConnectionString =
        "Host=localhost;Port=5432;Database=studentconnect_test_db;Username=agent_system;Password=Password@123;SSL Mode=Disable";

    public Database Database { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("STUDENTCONNECT_TEST_CONNECTION_STRING") ?? DefaultTestConnectionString;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = connectionString })
            .Build();
        Database = new Database(configuration);

        var databaseFolder = Path.Combine(FindRepoBackendRoot(), "database");
        var schemaSql = await File.ReadAllTextAsync(Path.Combine(databaseFolder, "schema.sql"));
        var seedSql = await File.ReadAllTextAsync(Path.Combine(databaseFolder, "seed.sql"));

        await Database.ExecuteAsync(schemaSql, bind: null);
        await Database.ExecuteAsync(seedSql, bind: null);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static string FindRepoBackendRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !Directory.Exists(Path.Combine(current.FullName, "database")))
        {
            current = current.Parent;
        }

        return current?.FullName
            ?? throw new InvalidOperationException("Could not locate the 'database' folder above the test output directory.");
    }
}

[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>;
