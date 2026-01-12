using Flare.Api.Filters;
using Flare.Application.Authorization;
using Flare.Application.Authorization.Requirements;
using Flare.Application.Interfaces;
using Flare.Application.Services;
using Flare.Domain.Enums;
using Microsoft.AspNetCore.Authentication.Cookies;

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
        services.AddScoped<IApiKeyValidator, ApiKeyValidator>();
        return services;
    }
}