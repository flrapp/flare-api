using Flare.Application.Authorization.Handlers;
using Flare.Application.Interfaces;
using Flare.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Flare.Application;

public static class ServiceCollectionRegistration
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPermissionService, PermissionService>();
        return services;
    }
    
    public static IServiceCollection AddAuthorizationHandler(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationHandler, AdminRequirementHandler>();
        services.AddSingleton<IAuthorizationHandler, ProjectAccessRequirementHandler>();
        services.AddSingleton<IAuthorizationHandler, ProjectOwnerRequirementHandler>();
        
        return services;
    }
}