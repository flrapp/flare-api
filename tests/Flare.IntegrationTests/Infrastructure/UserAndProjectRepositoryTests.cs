using Flare.Domain.Entities;
using Flare.Infrastructure.Data.Repositories.Implementation;
using Flare.IntegrationTests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Flare.IntegrationTests.Infrastructure;

public class UserAndProjectRepositoryTests(TestDatabase db) : IClassFixture<TestDatabase>
{
    #region Users

    [Fact]
    public async Task User_lookups_respect_active_flag()
    {
        var active = await Seed.UserAsync(db);
        var inactive = await Seed.UserAsync(db, active: false);
        await using var ctx = db.CreateContext();
        var repo = new UserRepository(ctx);

        Assert.Equal(active.Id, (await repo.GetByIdAsync(active.Id))!.Id);
        Assert.Equal(active.Id, (await repo.GetByUsernameAsync(active.Username))!.Id);
        Assert.NotNull(await repo.GetActiveByIdAsync(active.Id));
        Assert.NotNull(await repo.GetActiveByUsernameAsync(active.Username));
        Assert.Null(await repo.GetActiveByIdAsync(inactive.Id));
        Assert.Null(await repo.GetActiveByUsernameAsync(inactive.Username));
        Assert.True(await repo.ExistsByUsernameAsync(inactive.Username));
        Assert.False(await repo.ExistsByUsernameAsync("nobody_" + Guid.NewGuid()));

        var all = await repo.GetAllAsync();
        var activeOnly = await repo.GetAllActiveUsersAsync();
        Assert.Contains(all, u => u.Id == inactive.Id);
        Assert.DoesNotContain(activeOnly, u => u.Id == inactive.Id);
        Assert.Equal(all.Select(u => u.Username).Order(StringComparer.Ordinal), all.Select(u => u.Username), StringComparer.Ordinal);
    }

    [Fact]
    public async Task User_add_update_delete_round_trip()
    {
        var user = new User { Id = Guid.NewGuid(), Username = "rt_" + Guid.NewGuid().ToString("N")[..8], FullName = "A", PasswordHash = "h", CreatedAt = DateTime.UtcNow };

        await using (var ctx = db.CreateContext())
            await new UserRepository(ctx).AddAsync(user);

        await using (var ctx = db.CreateContext())
        {
            var repo = new UserRepository(ctx);
            var loaded = (await repo.GetByIdAsync(user.Id))!;
            loaded.FullName = "Renamed";
            await repo.UpdateAsync(loaded);
        }

        await using (var ctx = db.CreateContext())
        {
            var repo = new UserRepository(ctx);
            var loaded = (await repo.GetByIdAsync(user.Id))!;
            Assert.Equal("Renamed", loaded.FullName);
            await repo.DeleteAsync(loaded);
        }

        await using (var ctx = db.CreateContext())
            Assert.Null(await new UserRepository(ctx).GetByIdAsync(user.Id));
    }

    #endregion

    #region Projects

    [Fact]
    public async Task Project_lookups_by_id_alias_key_and_member()
    {
        var owner = await Seed.UserAsync(db);
        var project = await Seed.ProjectAsync(db, owner, false, "dev", "prod");
        var archived = await Seed.ProjectAsync(db, owner, archived: true);
        await Seed.MemberAsync(db, project, owner);
        await Seed.MemberAsync(db, archived, owner);
        await using var ctx = db.CreateContext();
        var repo = new ProjectRepository(ctx);

        Assert.NotNull(await repo.GetByIdAsync(project.Id));
        var byAlias = (await repo.GetByAliasAsync(project.Alias))!;
        Assert.Equal(2, byAlias.Scopes.Count);
        Assert.Equal(project.Id, (await repo.GetByApiKeyAsync(project.ApiKey))!.Id);
        Assert.True(await repo.ExistsByIdAsync(project.Id));
        Assert.True(await repo.ExistsByAliasAsync(project.Alias));
        Assert.True(await repo.ExistsByApiKeyAsync(project.ApiKey));
        Assert.False(await repo.ExistsByAliasAsync("missing"));

        var mine = await repo.GetByUserIdAsync(owner.Id);
        Assert.Equal([project.Id], mine.Select(p => p.Id));

        var visible = await repo.GetAllAsync();
        var everything = await repo.GetAllAsync(includeArchived: true);
        Assert.DoesNotContain(visible, p => p.Id == archived.Id);
        Assert.Contains(everything, p => p.Id == archived.Id);
    }

    [Fact]
    public async Task Project_update_and_delete()
    {
        var owner = await Seed.UserAsync(db);
        var project = await Seed.ProjectAsync(db, owner);

        await using (var ctx = db.CreateContext())
        {
            var repo = new ProjectRepository(ctx);
            var loaded = (await repo.GetByIdAsync(project.Id))!;
            loaded.Name = "Updated";
            await repo.UpdateAsync(loaded);
        }

        await using (var ctx = db.CreateContext())
        {
            var repo = new ProjectRepository(ctx);
            Assert.Equal("Updated", (await repo.GetByIdAsync(project.Id))!.Name);
            await repo.DeleteAsync(project.Id);
            await repo.DeleteAsync(Guid.NewGuid());
        }

        await using (var ctx = db.CreateContext())
        {
            Assert.False(await new ProjectRepository(ctx).ExistsByIdAsync(project.Id));
            Assert.False(await ctx.Scopes.AnyAsync(s => s.ProjectId == project.Id));
        }
    }

    [Fact]
    public async Task Project_add_persists_graph()
    {
        var owner = await Seed.UserAsync(db);
        var project = new Project
        {
            Id = Guid.NewGuid(), Alias = "new_" + Guid.NewGuid().ToString("N")[..6], Name = "New", ApiKey = Guid.NewGuid().ToString(),
            CreatedBy = owner.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        await using (var ctx = db.CreateContext())
            await new ProjectRepository(ctx).AddAsync(project);

        await using (var ctx = db.CreateContext())
            Assert.True(await new ProjectRepository(ctx).ExistsByIdAsync(project.Id));
    }

    [Fact]
    public async Task ProjectMember_repository_finds_membership()
    {
        var user = await Seed.UserAsync(db);
        var project = await Seed.ProjectAsync(db, user);
        await Seed.MemberAsync(db, project, user);
        await using var ctx = db.CreateContext();
        var repo = new ProjectMemberRepository(ctx);

        Assert.NotNull(await repo.GetByUserAndProjectAsync(user.Id, project.Id));
        Assert.True(await repo.ExistsAsync(user.Id, project.Id));
        Assert.False(await repo.ExistsAsync(Guid.NewGuid(), project.Id));
    }

    #endregion

    #region Scopes

    [Fact]
    public async Task Scope_queries_and_mutations()
    {
        var owner = await Seed.UserAsync(db);
        var project = await Seed.ProjectAsync(db, owner, false, "dev", "prod");
        var flag = await Seed.FlagAsync(db, project);
        var dev = project.Scopes.Single(s => s.Alias == "dev");

        await using (var ctx = db.CreateContext())
        {
            var repo = new ScopeRepository(ctx);
            Assert.NotNull(await repo.GetByIdAsync(dev.Id));
            Assert.Equal(project.Alias, (await repo.GetByIdWithProjectAsync(dev.Id))!.Project.Alias);
            var detailed = (await repo.GetByIdWithDetailsAsync(dev.Id))!;
            Assert.Equal(flag.Key, Assert.Single(detailed.FeatureFlagValues).FeatureFlag.Key);
            Assert.Equal(dev.Id, (await repo.GetByProjectAndAliasAsync(project.Id, "dev"))!.Id);
            Assert.Equal(["dev", "prod"], (await repo.GetByProjectIdAsync(project.Id)).Select(s => s.Alias));
            Assert.True(await repo.ExistsByIdAsync(dev.Id));
            Assert.True(await repo.ExistsByProjectAndAliasAsync(project.Id, "prod"));
            Assert.False(await repo.ExistsByProjectAndAliasExcludingIdAsync(project.Id, "dev", dev.Id));
            Assert.True(await repo.ExistsByProjectAndAliasExcludingIdAsync(project.Id, "prod", dev.Id));

            var qa = new Scope { Id = Guid.NewGuid(), ProjectId = project.Id, Alias = "qa", Name = "QA", Index = 5, CreatedAt = DateTime.UtcNow };
            await repo.AddAsync(qa);
        }

        await using (var ctx = db.CreateContext())
        {
            var repo = new ScopeRepository(ctx);
            var qa = (await repo.GetByProjectAndAliasAsync(project.Id, "qa"))!;
            qa.Name = "Quality";
            await repo.UpdateAsync(qa);
            await repo.DeleteAsync(dev.Id);
            await repo.DeleteAsync(Guid.NewGuid());
        }

        await using (var ctx = db.CreateContext())
        {
            var repo = new ScopeRepository(ctx);
            Assert.Equal("Quality", (await repo.GetByProjectAndAliasAsync(project.Id, "qa"))!.Name);
            Assert.False(await repo.ExistsByIdAsync(dev.Id));
        }
    }

    #endregion

    #region UnitOfWork

    [Fact]
    public async Task UnitOfWork_commit_persists_changes()
    {
        var username = "uow_" + Guid.NewGuid().ToString("N")[..8];
        await using (var ctx = db.CreateContext())
        {
            await using var uow = new UnitOfWork(ctx);
            await uow.BeginTransactionAsync();
            await new UserRepository(ctx).AddAsync(new User { Id = Guid.NewGuid(), Username = username, FullName = "U", PasswordHash = "h", CreatedAt = DateTime.UtcNow });
            await uow.CommitAsync();
        }

        await using (var ctx = db.CreateContext())
            Assert.True(await new UserRepository(ctx).ExistsByUsernameAsync(username));
    }

    [Fact]
    public async Task UnitOfWork_rollback_discards_changes()
    {
        var username = "uow_" + Guid.NewGuid().ToString("N")[..8];
        await using (var ctx = db.CreateContext())
        {
            await using var uow = new UnitOfWork(ctx);
            await uow.BeginTransactionAsync();
            await new UserRepository(ctx).AddAsync(new User { Id = Guid.NewGuid(), Username = username, FullName = "U", PasswordHash = "h", CreatedAt = DateTime.UtcNow });
            await uow.RollbackAsync();
        }

        await using (var ctx = db.CreateContext())
            Assert.False(await new UserRepository(ctx).ExistsByUsernameAsync(username));
    }

    [Fact]
    public async Task UnitOfWork_without_transaction()
    {
        await using var ctx = db.CreateContext();
        await using var uow = new UnitOfWork(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(() => uow.CommitAsync());
        await uow.RollbackAsync();
    }

    #endregion
}
