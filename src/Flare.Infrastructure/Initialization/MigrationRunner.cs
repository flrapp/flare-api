using Flare.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Flare.Infrastructure.Initialization;

public class MigrationRunner
{
    // Unique lock key for this application — must not collide with other services on the same PG server
    private const long LockKey = 7368190231847293L;

    private readonly string _connectionString;
    private readonly IServiceProvider _serviceProvider;
    private readonly int _timeoutSeconds;
    private readonly int _pollingIntervalMs;
    private readonly ILogger<MigrationRunner> _logger;

    public MigrationRunner(
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        ILogger<MigrationRunner> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured");
        _serviceProvider = serviceProvider;
        _logger = logger;

        var section = configuration.GetSection("MigrationLock");
        _timeoutSeconds = int.TryParse(section["TimeoutSeconds"], out var t) ? t : 120;
        _pollingIntervalMs = int.TryParse(section["PollingIntervalMs"], out var p) ? p : 500;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        // Use a dedicated connection outside the EF pool so the advisory lock
        // is tied to a single stable session for the duration of the migration.
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        _logger.LogInformation(
            "Attempting to acquire PostgreSQL advisory lock {LockKey} for database migration",
            LockKey);

        var acquired = false;
        var deadline = DateTime.UtcNow.AddSeconds(_timeoutSeconds);

        try
        {
            while (true)
            {
                acquired = await TryAcquireLockAsync(connection, cancellationToken);

                if (acquired)
                {
                    _logger.LogInformation("Advisory lock {LockKey} acquired, proceeding with migration", LockKey);
                    break;
                }

                if (DateTime.UtcNow >= deadline)
                {
                    _logger.LogError(
                        "Timed out waiting for advisory lock {LockKey} after {TimeoutSeconds}s",
                        LockKey, _timeoutSeconds);

                    throw new TimeoutException(
                        $"Could not acquire PostgreSQL advisory lock {LockKey} within {_timeoutSeconds} seconds. " +
                        "Another instance may be holding the lock indefinitely.");
                }

                _logger.LogInformation(
                    "Advisory lock {LockKey} is held by another instance, retrying in {PollingIntervalMs}ms",
                    LockKey, _pollingIntervalMs);

                await Task.Delay(_pollingIntervalMs, cancellationToken);
            }

            _logger.LogInformation("Starting database migration");

            // Create a fresh scope so the DbContext is only instantiated after the
            // lock is held — avoids EF startup work before migrations are safe to run.
            await using var scope = _serviceProvider.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await dbContext.Database.MigrateAsync(cancellationToken);

            _logger.LogInformation("Database migration completed successfully");
        }
        finally
        {
            if (acquired)
            {
                await ReleaseLockAsync(connection);
                _logger.LogInformation("Advisory lock {LockKey} released", LockKey);
            }
        }
    }

    private static async Task<bool> TryAcquireLockAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using var cmd = new NpgsqlCommand("SELECT pg_try_advisory_lock(@key)", connection);
        cmd.Parameters.AddWithValue("key", LockKey);
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is true;
    }

    private static async Task ReleaseLockAsync(NpgsqlConnection connection)
    {
        await using var cmd = new NpgsqlCommand("SELECT pg_advisory_unlock(@key)", connection);
        cmd.Parameters.AddWithValue("key", LockKey);
        await cmd.ExecuteScalarAsync();
    }
}
