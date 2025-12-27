using Flare.Infrastructure.Data;
using Flare.Infrastructure.Data.Repositories;
using Flare.Infrastructure.Data.Repositories.Implementation;
using Flare.Infrastructure.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flare.Infrastructure;

public static class ServiceCollectionRegistration
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IProjectMemberRepository, ProjectMemberRepository>();

        return services;
    }
}