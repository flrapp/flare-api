using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Domian.Enums;
using Flare.Api.Extensions;
using Flare.Api.Middleware;
using Flare.Application;
using Flare.Application.Authorization;
using Flare.Application.Authorization.Requirements;
using Flare.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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

        services.AddServices();
        services.AddAuthorizationHandler();
        services.AddApplicationAuth();
        
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

        app.UseAuthentication();
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
