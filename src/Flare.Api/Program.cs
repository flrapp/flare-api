using Flare.Infrastructure.Initialization;
using Flare.Infrastructure.Observability;
using Serilog;

namespace Flare.Api;

public class Program
{
    public static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .Enrich.FromLogContext()
            .CreateBootstrapLogger();

        try
        {
            Log.Information("Starting web application");
            var host = CreateHostBuilder(args).Build();

            using (var scope = host.Services.CreateScope())
            {
                var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
                await initializer.InitializeAsync();
            }

            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((_, config) =>
            {
                config.AddEnvironmentVariables("FLARE_ADMIN_");
                config.AddEnvironmentVariables("FLARE_CORS_");
            })
            .UseSerilog((context, services, configuration) =>
                LoggingConfiguration.ConfigureSerilog(configuration, context.Configuration, services))
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
            });
}