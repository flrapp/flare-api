using Flare.Application.Audit;
using Flare.Application.Authorization.Handlers;
using Flare.Application.Interfaces;
using Flare.Application.Metrics;
using Flare.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Flare.Application;

public static class ServiceCollectionRegistration
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddSingleton<IAuditLogger, SerilogAuditLogger>();
        services.AddSingleton<FlareMetrics>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IScopeService, ScopeService>();
        services.AddScoped<IFeatureFlagService, FeatureFlagService>();
        services.AddScoped<IProjectUserService, ProjectUserService>();
        return services;
    }
    
    public static IServiceCollection AddAuthorizationHandler(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationHandler, AdminRequirementHandler>();
        services.AddScoped<IAuthorizationHandler, ProjectPermissionRequirementHandler>();
        services.AddScoped<IAuthorizationHandler, ScopePermissionRequirementHandler>();

        return services;
    }
}