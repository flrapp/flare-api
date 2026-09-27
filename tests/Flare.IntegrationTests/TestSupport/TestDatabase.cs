using Flare.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Flare.IntegrationTests.TestSupport;

/// Class fixture: an isolated, fully migrated database for one test class.
public sealed class TestDatabase : IAsyncLifetime
{
    public string ConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        ConnectionString = await CreateEmptyAsync();
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public ApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options);

    /// Creates a new empty database on the shared server and returns its connection string.
    public static async Task<string> CreateEmptyAsync()
    {
        var serverConnectionString = await PostgresServer.GetConnectionStringAsync();
        var name = "flare_" + Guid.NewGuid().ToString("N");

        await using (var connection = new NpgsqlConnection(serverConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
            await command.ExecuteNonQueryAsync();
        }

        return new NpgsqlConnectionStringBuilder(serverConnectionString) { Database = name }.ConnectionString;
    }
}
