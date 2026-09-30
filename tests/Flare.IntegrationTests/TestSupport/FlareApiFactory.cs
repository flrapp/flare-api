using Flare.Api;
using Flare.Infrastructure.Initialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flare.IntegrationTests.TestSupport;

/// Boots the real API (Startup + Program host configuration) against an isolated database,
/// then runs the same startup sequence as Program.Main: migrations, then admin seeding.
public class FlareApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminUsername = "admin";
    public const string AdminPassword = "Admin12345";

    private string _connectionString = string.Empty;

    protected virtual int SdkGlobalPermitsPerSecond => 10_000;
    protected virtual int SdkPerKeyPermitsPerSecond => 10_000;

    public async ValueTask InitializeAsync()
    {
        _connectionString = await TestDatabase.CreateEmptyAsync();

        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<MigrationRunner>().RunAsync();
        await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = _connectionString,
            ["ADMIN_USERNAME"] = AdminUsername,
            ["ADMIN_PASSWORD"] = AdminPassword,
            ["RateLimiting:Global:PermitsPerSecond"] = SdkGlobalPermitsPerSecond.ToString(),
            ["RateLimiting:PerProject:PermitsPerSecond"] = SdkPerKeyPermitsPerSecond.ToString(),
            ["Serilog:MinimumLevel:Default"] = "Warning",
            ["Serilog:MinimumLevel:Override:Flare.Api.Middleware"] = "Fatal"
        }));
    }

    /// Cookies are issued with the Secure flag, so the client must talk HTTPS to keep them.
    public HttpClient CreateHttpsClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false
    });
}
