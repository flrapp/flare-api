using Domian.Enums;
using Flare.Application.Authorization;
using Flare.Application.Authorization.Requirements;
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

            options.AddPolicy(AuthorizationPolicies.ProjectViewer, policy =>
                policy.Requirements.Add(new ProjectAccessRequirement(ProjectRole.Viewer)));

            options.AddPolicy(AuthorizationPolicies.ProjectEditor, policy =>
                policy.Requirements.Add(new ProjectAccessRequirement(ProjectRole.Editor)));

            options.AddPolicy(AuthorizationPolicies.ProjectOwner, policy =>
                policy.Requirements.Add(new ProjectOwnerRequirement()));
        });

        return services;
    }
}