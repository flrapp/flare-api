using Flare.Domain.Entities;
using Flare.Domain.Enums;
using Flare.Infrastructure.Data;

namespace Flare.IntegrationTests.TestSupport;

/// Inserts minimal object graphs directly through EF for repository tests.
internal static class Seed
{
    public static async Task<User> UserAsync(TestDatabase db, string? username = null, bool active = true, GlobalRole role = GlobalRole.User)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username ?? "user_" + Guid.NewGuid().ToString("N")[..8],
            FullName = "Test User",
            PasswordHash = "hash",
            GlobalRole = role,
            IsActive = active,
            CreatedAt = DateTime.UtcNow
        };
        await SaveAsync(db, ctx => ctx.Users.Add(user));
        return user;
    }

    public static async Task<Project> ProjectAsync(TestDatabase db, User creator, bool archived = false, params string[] scopeAliases)
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Alias = "p_" + Guid.NewGuid().ToString("N")[..8],
            Name = "Project",
            ApiKey = Guid.NewGuid().ToString("N"),
            CreatedBy = creator.Id,
            IsArchived = archived,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var index = 0;
        foreach (var alias in scopeAliases.Length > 0 ? scopeAliases : ["dev"])
        {
            project.Scopes.Add(new Scope
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                Alias = alias,
                Name = alias,
                Index = index++,
                CreatedAt = DateTime.UtcNow
            });
        }
        await SaveAsync(db, ctx => ctx.Projects.Add(project));
        return project;
    }

    public static async Task<FeatureFlag> FlagAsync(TestDatabase db, Project project, string key = "flag",
        FeatureFlagType type = FeatureFlagType.Boolean, string name = "Flag")
    {
        var flag = new FeatureFlag
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Key = key,
            Name = name,
            Type = type,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        foreach (var scope in project.Scopes)
            flag.Values.Add(flag.CreateValueForScope(scope.Id));
        await SaveAsync(db, ctx => ctx.FeatureFlags.Add(flag));
        return flag;
    }

    public static async Task<ProjectUser> MemberAsync(TestDatabase db, Project project, User user)
    {
        var member = new ProjectUser
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            UserId = user.Id,
            InvitedBy = user.Id,
            JoinedAt = DateTime.UtcNow
        };
        await SaveAsync(db, ctx => ctx.ProjectUsers.Add(member));
        return member;
    }

    public static async Task SaveAsync(TestDatabase db, Action<ApplicationDbContext> arrange)
    {
        await using var context = db.CreateContext();
        arrange(context);
        await context.SaveChangesAsync();
    }
}
