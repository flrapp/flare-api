using Flare.Domain.Enums;
using Flare.Infrastructure.Data.Repositories.Implementation;
using Flare.IntegrationTests.TestSupport;

namespace Flare.IntegrationTests.Infrastructure;

public class ProjectUserRepositoryTests(TestDatabase db) : IClassFixture<TestDatabase>
{
    [Fact]
    public async Task Project_permissions_add_query_and_remove()
    {
        var user = await Seed.UserAsync(db);
        var project = await Seed.ProjectAsync(db, user);
        var member = await Seed.MemberAsync(db, project, user);

        await using (var ctx = db.CreateContext())
        {
            var repo = new ProjectUserRepository(ctx);
            await repo.AddProjectPermissionAsync(member.Id, ProjectPermission.ManageUsers);
            await repo.AddProjectPermissionAsync(member.Id, ProjectPermission.ManageUsers);
            await repo.AddProjectPermissionsAsync(member.Id, [ProjectPermission.ManageUsers, ProjectPermission.ViewApiKey, ProjectPermission.ViewApiKey]);
        }

        await using (var ctx = db.CreateContext())
        {
            var repo = new ProjectUserRepository(ctx);
            var permissions = await repo.GetUserProjectPermissionsAsync(user.Id, project.Id);
            Assert.Equal([ProjectPermission.ManageUsers, ProjectPermission.ViewApiKey], permissions.Order());
            Assert.True(await repo.HasProjectPermissionAsync(user.Id, project.Id, ProjectPermission.ViewApiKey));
            Assert.False(await repo.HasProjectPermissionAsync(user.Id, project.Id, ProjectPermission.DeleteProject));

            await repo.RemoveProjectPermissionAsync(member.Id, ProjectPermission.ViewApiKey);
            await repo.RemoveProjectPermissionAsync(member.Id, ProjectPermission.DeleteProject);
            Assert.Equal([ProjectPermission.ManageUsers], await repo.GetUserProjectPermissionsAsync(user.Id, project.Id));

            await repo.RemoveAllProjectPermissionsAsync(member.Id);
            await repo.RemoveAllProjectPermissionsAsync(member.Id);
            Assert.Empty(await repo.GetUserProjectPermissionsAsync(user.Id, project.Id));
        }
    }

    [Fact]
    public async Task Scope_permissions_add_query_and_remove()
    {
        var user = await Seed.UserAsync(db);
        var project = await Seed.ProjectAsync(db, user, false, "dev", "prod");
        var member = await Seed.MemberAsync(db, project, user);
        var dev = project.Scopes.Single(s => s.Alias == "dev").Id;
        var prod = project.Scopes.Single(s => s.Alias == "prod").Id;

        await using (var ctx = db.CreateContext())
        {
            var repo = new ProjectUserRepository(ctx);
            await repo.AddScopePermissionAsync(member.Id, dev, ScopePermission.ReadFeatureFlags);
            await repo.AddScopePermissionAsync(member.Id, dev, ScopePermission.ReadFeatureFlags);
            await repo.AddScopePermissionsAsync(member.Id, new Dictionary<Guid, IEnumerable<ScopePermission>>());
            await repo.AddScopePermissionsAsync(member.Id, new Dictionary<Guid, IEnumerable<ScopePermission>>
            {
                [dev] = [ScopePermission.ReadFeatureFlags, ScopePermission.UpdateFeatureFlags],
                [prod] = [ScopePermission.ReadFeatureFlags, ScopePermission.ReadFeatureFlags]
            });
        }

        await using (var ctx = db.CreateContext())
        {
            var repo = new ProjectUserRepository(ctx);
            var permissions = await repo.GetUserScopePermissionsAsync(user.Id, project.Id);
            Assert.Equal(2, permissions[dev].Count);
            Assert.Equal([ScopePermission.ReadFeatureFlags], permissions[prod]);
            Assert.True(await repo.HasScopePermissionAsync(user.Id, dev, ScopePermission.UpdateFeatureFlags));
            Assert.False(await repo.HasScopePermissionAsync(user.Id, prod, ScopePermission.UpdateFeatureFlags));

            await repo.RemoveScopePermissionAsync(member.Id, dev, ScopePermission.UpdateFeatureFlags);
            await repo.RemoveScopePermissionAsync(member.Id, dev, ScopePermission.UpdateFeatureFlags);
            Assert.Equal([ScopePermission.ReadFeatureFlags], (await repo.GetUserScopePermissionsAsync(user.Id, project.Id))[dev]);

            await repo.RemoveAllScopePermissionsForScopeAsync(prod);
            await repo.RemoveAllScopePermissionsForScopeAsync(prod);
            Assert.False((await repo.GetUserScopePermissionsAsync(user.Id, project.Id)).ContainsKey(prod));

            await repo.RemoveAllScopePermissionsAsync(member.Id);
            await repo.RemoveAllScopePermissionsAsync(member.Id);
            Assert.Empty(await repo.GetUserScopePermissionsAsync(user.Id, project.Id));
        }
    }

    [Fact]
    public async Task Membership_lookups_and_lifecycle()
    {
        var owner = await Seed.UserAsync(db);
        var other = await Seed.UserAsync(db);
        var project = await Seed.ProjectAsync(db, owner);
        var ownerMembership = await Seed.MemberAsync(db, project, owner);

        await using (var ctx = db.CreateContext())
        {
            var repo = new ProjectUserRepository(ctx);
            await repo.AddAsync(new Flare.Domain.Entities.ProjectUser
            {
                Id = Guid.NewGuid(), ProjectId = project.Id, UserId = other.Id, InvitedBy = owner.Id, JoinedAt = DateTime.UtcNow.AddMinutes(1)
            });
        }

        await using (var ctx = db.CreateContext())
        {
            var repo = new ProjectUserRepository(ctx);
            Assert.NotNull(await repo.GetByIdAsync(ownerMembership.Id));
            Assert.Equal(owner.Username, (await repo.GetByUserAndProjectAsync(owner.Id, project.Id))!.User.Username);
            Assert.NotNull(await repo.GetByUserAndProjectWithPermissionsAsync(other.Id, project.Id));
            Assert.Equal([owner.Id, other.Id], (await repo.GetByProjectIdAsync(project.Id)).Select(pu => pu.UserId));
            Assert.True(await repo.ExistsAsync(other.Id, project.Id));
            Assert.True(await repo.ExistsByIdAsync(ownerMembership.Id));

            var loaded = (await repo.GetByIdAsync(ownerMembership.Id))!;
            loaded.JoinedAt = loaded.JoinedAt.AddDays(-1);
            await repo.UpdateAsync(loaded);

            await repo.DeleteAsync(ownerMembership.Id);
            await repo.DeleteAsync(Guid.NewGuid());
        }

        await using (var ctx = db.CreateContext())
        {
            var repo = new ProjectUserRepository(ctx);
            Assert.False(await repo.ExistsAsync(owner.Id, project.Id));
            Assert.Single(await repo.GetByProjectIdAsync(project.Id));
        }
    }
}
