using Flare.Infrastructure.Initialization;
using Serilog;
using Serilog.Sinks.OpenTelemetry;

namespace Flare.Api;

public class Program
{
    public static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
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
            await Log.CloseAndFlushAsync();
        }
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((_, config) =>
            {
                config.AddEnvironmentVariables("FLARE_");
            })
            .UseSerilog((context, services, configuration) =>
            {
                configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext()
                    .WriteTo.Console();
                    
                var otelEndpoint = context.Configuration.GetValue<string>("OTEL_ENDPOINT");
                if (otelEndpoint != null)
                {
                    configuration.WriteTo.OpenTelemetry(opts =>
                    {
                        opts.Endpoint = otelEndpoint; 
                        opts.Protocol = OtlpProtocol.HttpProtobuf;
                        opts.ResourceAttributes = new Dictionary<string, object>
                        {
                            ["service.name"] = "flare-api"
                        };
                    }); 
                }
            })
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
            });
}