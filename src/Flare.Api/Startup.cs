using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Flare.Api.Constants;
using Flare.Api.Extensions;
using Flare.Api.Middleware;
using Flare.Application;
using Flare.Infrastructure;
using Flare.Infrastructure.Data;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Scalar.AspNetCore;

namespace Flare.Api;

public class Startup
{
    public Startup(IConfiguration configuration, IWebHostEnvironment environment)
    {
        Configuration = configuration;
        Environment = environment;
    }

    private IConfiguration Configuration { get; }
    private IWebHostEnvironment Environment { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddInfrastructure(Configuration);

        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        services.AddServices();
        services.ConfigureHybridCache();
        services.AddAuthorizationHandler();
        services.AddProjectApiKeyAuthorisation();
        services.AddApplicationAuth();
        services.AddHttpContextAccessor();
        services.AddControllers();

        services.ConfigureTracingAndMetrics(Configuration);
        services.ConfigureRateLimiting(Configuration);

        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        }).AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

        if (Environment.IsDevelopment())
        {
            services.AddOpenApi();
        }
   
        services.ConfigureCors(Configuration, Environment);

        services.AddHealthChecks()
            .AddDbContextCheck<ApplicationDbContext>("database", tags: ["ready"]);
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IApiVersionDescriptionProvider apiVersionDescriptionProvider)
    {
        app.UseExceptionHandler();

        app.UseRouting();
        app.UseCors(CorsPolicyConstants.UiCorsPolicy);
        app.UseHttpsRedirection(); 

        app.UseRateLimiter();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapPrometheusScrapingEndpoint("/metrics");
            endpoints.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
            endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("ready")
            });
            endpoints.MapControllers();

            if (env.IsDevelopment())
            {
                foreach (var description in apiVersionDescriptionProvider.ApiVersionDescriptions)
                {
                    endpoints.MapOpenApi($"/openapi/{description.GroupName}.json");
                }

                endpoints.MapScalarApiReference(options =>
                {
                    options.WithTitle("Flare.Api")
                           .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
                });
            }
        });
    }
}
