using System;
using System.Threading.Tasks;
using Flare.Api.Constants;
using Flare.Api.Filters;
using Flare.Application.Authorization;
using Flare.Application.Authorization.Requirements;
using Flare.Application.Interfaces;
using Flare.Application.Services;
using Flare.Domain.Enums;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Flare.Api.Extensions;

public static class ServiceCollectionRegistration
{
    public static IServiceCollection AddApplicationAuth(this IServiceCollection services)
    {
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "FlareAuth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.ExpireTimeSpan = TimeSpan.FromDays(7);
                options.SlidingExpiration = true;
                options.LoginPath = "/api/v1/auth/login";
                options.LogoutPath = "/api/v1/auth/logout";
                options.AccessDeniedPath = "/api/v1/auth/access-denied";
                options.Events = new CookieAuthenticationEvents
                {
                    OnRedirectToLogin = context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    },
                    OnRedirectToAccessDenied = context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.AdminOnly, policy =>
                policy.Requirements.Add(new AdminRequirement()));

            // Project-level permission policies
            options.AddPolicy(AuthorizationPolicies.ManageProjectSettings, policy =>
                policy.Requirements.Add(new ProjectPermissionRequirement(ProjectPermission.ManageProjectSettings)));
            options.AddPolicy(AuthorizationPolicies.ManageUsers, policy =>
                policy.Requirements.Add(new ProjectPermissionRequirement(ProjectPermission.ManageUsers)));
            options.AddPolicy(AuthorizationPolicies.ManageScopes, policy =>
                policy.Requirements.Add(new ProjectPermissionRequirement(ProjectPermission.ManageScopes)));
            options.AddPolicy(AuthorizationPolicies.ManageFeatureFlags, policy =>
                policy.Requirements.Add(new ProjectPermissionRequirement(ProjectPermission.ManageFeatureFlags)));
            options.AddPolicy(AuthorizationPolicies.ViewApiKey, policy =>
                policy.Requirements.Add(new ProjectPermissionRequirement(ProjectPermission.ViewApiKey)));
            options.AddPolicy(AuthorizationPolicies.RegenerateApiKey, policy =>
                policy.Requirements.Add(new ProjectPermissionRequirement(ProjectPermission.RegenerateApiKey)));
            options.AddPolicy(AuthorizationPolicies.DeleteProject, policy =>
                policy.Requirements.Add(new ProjectPermissionRequirement(ProjectPermission.DeleteProject)));

            // Scope-level permission policies
            options.AddPolicy(AuthorizationPolicies.ReadFeatureFlags, policy =>
                policy.Requirements.Add(new ScopePermissionRequirement(ScopePermission.ReadFeatureFlags)));
            options.AddPolicy(AuthorizationPolicies.UpdateFeatureFlags, policy =>
                policy.Requirements.Add(new ScopePermissionRequirement(ScopePermission.UpdateFeatureFlags)));
        });

        return services;
    }
    
    public static IServiceCollection ConfigureHybridCache(this IServiceCollection services)
    {
        services.AddHybridCache();
        return services;
    }

    public static IServiceCollection AddProjectApiKeyAuthorisation(this IServiceCollection services)
    {
        services.AddScoped<ProjectApiKeyAuthorizationFilter>();
        services.AddScoped<BearerApiKeyAuthorizationFilter>();
        services.AddScoped<IApiKeyValidator, ApiKeyValidator>();
        return services;
    }

    public static IServiceCollection ConfigureCors(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicyConstants.UiCorsPolicy, builder =>
            {
                if (environment.IsDevelopment())
                {
                    builder.WithOrigins("http://localhost:3000")
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials(); 
                }
                else
                {
                    var corsOrigins = configuration.GetSection("CORS_ALLOWED_ORIGINS").Get<string>();
                    if(string.IsNullOrEmpty(corsOrigins))
                        throw new ArgumentException("ALLOWED_ORIGINS section not found");

                    Console.WriteLine($"[CORS] Allowed origins: {corsOrigins}");
                    builder.WithOrigins(corsOrigins) 
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials();
                }
            });
            
            options.AddPolicy(CorsPolicyConstants.IntegrationCorsPolicy, builder =>
            {
                builder.SetIsOriginAllowed(_ => true)
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
        });

        return services;
    }

    public static IServiceCollection ConfigureTracingAndMetrics(this IServiceCollection services, IConfiguration configuration)
    {
        var otelEndpoint = configuration.GetValue<string>(EnvironmentVariablesNames.OtelEndpoint);

        if (!string.IsNullOrWhiteSpace(otelEndpoint))
        {
            services.AddOpenTelemetry()
                .WithTracing(tracing => tracing
                    .AddAspNetCoreInstrumentation()
                    .AddOtlpExporter(opts =>
                    {
                        opts.Endpoint = new Uri(otelEndpoint);
                        opts.Protocol = OtlpExportProtocol.HttpProtobuf;
                    }))
                .WithMetrics(metrics => metrics
                    .AddAspNetCoreInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddOtlpExporter(opts =>
                    {
                        opts.Endpoint = new Uri(otelEndpoint);
                        opts.Protocol = OtlpExportProtocol.HttpProtobuf;
                    })
                );
        }
        return services;
    }
}