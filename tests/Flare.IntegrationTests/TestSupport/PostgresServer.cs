using Testcontainers.PostgreSql;

namespace Flare.IntegrationTests.TestSupport;

/// One PostgreSQL container shared by the whole test run; each test class gets its own database in it.
/// Testcontainers' resource reaper removes the container when the test process exits.
internal static class PostgresServer
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static PostgreSqlContainer? _container;

    public static async Task<string> GetConnectionStringAsync()
    {
        if (_container is not null)
            return _container.GetConnectionString();

        await Gate.WaitAsync();
        try
        {
            if (_container is null)
            {
                var container = new PostgreSqlBuilder()
                    .WithImage("postgres:17-alpine")
                    .Build();
                await container.StartAsync();
                _container = container;
            }

            return _container.GetConnectionString();
        }
        finally
        {
            Gate.Release();
        }
    }
}
