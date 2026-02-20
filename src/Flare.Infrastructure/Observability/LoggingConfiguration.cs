using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Flare.Infrastructure.Observability;

/// <summary>
/// Static configuration methods for Serilog setup.
/// </summary>
public static class LoggingConfiguration
{
    /// <summary>
    /// Configures Serilog with the application's logging settings.
    /// </summary>
    public static void ConfigureSerilog(
        LoggerConfiguration loggerConfiguration,
        IConfiguration configuration,
        IServiceProvider services)
    {
        loggerConfiguration
            .ReadFrom.Configuration(configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.WithEnvironmentName();
    }
}
