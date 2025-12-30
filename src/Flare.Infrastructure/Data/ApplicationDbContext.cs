using Flare.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Flare.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<Project> Projects { get; set; }
    public DbSet<ProjectUser> ProjectUsers { get; set; }
    public DbSet<Invitation> Invitations { get; set; }
    public DbSet<Scope> Scopes { get; set; }
    public DbSet<FeatureFlag> FeatureFlags { get; set; }
    public DbSet<FeatureFlagValue> FeatureFlagValues { get; set; }
    public DbSet<ProjectUserProjectPermission> ProjectUserProjectPermissions { get; set; }
    public DbSet<ProjectUserScopePermission> ProjectUserScopePermissions { get; set; }
    
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}