using Flare.Domain.Enums;
using Flare.Infrastructure;
using Flare.Infrastructure.Data;
using Flare.Infrastructure.Initialization;
using Flare.IntegrationTests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Flare.IntegrationTests.Infrastructure;

public class InitializationTests
{
    private static IConfiguration Config(string? connectionString, Dictionary<string, string?>? extra = null)
    {
        var values = new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = connectionString };
        foreach (var (key, value) in extra ?? [])
            values[key] = value;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static ServiceProvider BuildProvider(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(configuration);
        services.AddInfrastructure(configuration);
        return services.BuildServiceProvider();
    }

    #region MigrationRunner

    [Fact]
    public async Task MigrationRunner_applies_all_migrations()
    {
        var connectionString = await TestDatabase.CreateEmptyAsync();
        await using var provider = BuildProvider(Config(connectionString));
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<MigrationRunner>().RunAsync();

        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.NotEmpty(await context.Database.GetAppliedMigrationsAsync());
    }

    [Fact]
    public async Task MigrationRunner_times_out_when_lock_is_held()
    {
        var connectionString = await TestDatabase.CreateEmptyAsync();
        await using var holder = new NpgsqlConnection(connectionString);
        await holder.OpenAsync();
        await using (var cmd = new NpgsqlCommand("SELECT pg_advisory_lock(7368190231847293)", holder))
            await cmd.ExecuteScalarAsync();

        var config = Config(connectionString, new()
        {
            ["MigrationLock:TimeoutSeconds"] = "1",
            ["MigrationLock:PollingIntervalMs"] = "100"
        });
        var runner = new MigrationRunner(config, BuildProvider(config), NullLogger<MigrationRunner>.Instance);

        var ex = await Assert.ThrowsAsync<TimeoutException>(() => runner.RunAsync());

        Assert.Contains("advisory lock", ex.Message);
    }

    [Fact]
    public void MigrationRunner_requires_connection_string()
    {
        var config = new ConfigurationBuilder().Build();

        Assert.Throws<InvalidOperationException>(() =>
            new MigrationRunner(config, new ServiceCollection().BuildServiceProvider(), NullLogger<MigrationRunner>.Instance));
    }

    #endregion

    #region DatabaseInitializer

    private static DatabaseInitializer Initializer(TestDatabase db, Dictionary<string, string?> settings, out ApplicationDbContext context)
    {
        context = db.CreateContext();
        return new DatabaseInitializer(context, Config(db.ConnectionString, settings), NullLogger<DatabaseInitializer>.Instance);
    }

    private static async Task<TestDatabase> FreshDatabase()
    {
        var db = new TestDatabase();
        await db.InitializeAsync();
        return db;
    }

    [Fact]
    public async Task Initializer_seeds_super_admin_once()
    {
        var db = await FreshDatabase();
        var settings = new Dictionary<string, string?> { ["ADMIN_USERNAME"] = "root", ["ADMIN_PASSWORD"] = "Str0ngPass" };

        await Initializer(db, settings, out var first).InitializeAsync();
        await Initializer(db, settings, out var second).InitializeAsync();

        await using var check = db.CreateContext();
        var admin = await check.Users.SingleAsync();
        Assert.Equal("root", admin.Username);
        Assert.Equal("System Administrator", admin.FullName);
        Assert.Equal(GlobalRole.SuperAdmin, admin.GlobalRole);
        Assert.True(admin.InitialUser);
        Assert.True(BCrypt.Net.BCrypt.Verify("Str0ngPass", admin.PasswordHash));
        await first.DisposeAsync();
        await second.DisposeAsync();
    }

    [Theory]
    [InlineData(null, "Str0ngPass", "No admin credentials")]
    [InlineData("root", null, "No admin credentials")]
    [InlineData("root", "short1A", "security requirements")]
    [InlineData("root", "alllowercase1", "security requirements")]
    [InlineData("root", "ALLUPPERCASE1", "security requirements")]
    [InlineData("root", "NoDigitsHere", "security requirements")]
    public async Task Initializer_rejects_missing_or_weak_credentials(string? username, string? password, string message)
    {
        var db = await FreshDatabase();
        var initializer = Initializer(db, new() { ["ADMIN_USERNAME"] = username, ["ADMIN_PASSWORD"] = password }, out var context);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => initializer.InitializeAsync());

        Assert.Contains(message, ex.Message);
        await context.DisposeAsync();
    }

    #endregion
}
