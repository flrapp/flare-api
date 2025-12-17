using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Flare.Api.Middleware;
using Flare.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Scalar.AspNetCore;

namespace Flare.Api;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddInfrastructure(Configuration);

        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        services.AddAuthorization();

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

        services.AddOpenApi();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IApiVersionDescriptionProvider apiVersionDescriptionProvider)
    {
        app.UseExceptionHandler();

        app.UseHttpsRedirection();
        app.UseRouting();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
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
