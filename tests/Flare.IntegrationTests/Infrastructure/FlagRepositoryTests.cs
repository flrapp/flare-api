using Flare.Domain.Entities;
using Flare.Domain.Enums;
using Flare.Infrastructure.Data.Repositories.Implementation;
using Flare.IntegrationTests.TestSupport;

namespace Flare.IntegrationTests.Infrastructure;

public class FlagRepositoryTests(TestDatabase db) : IClassFixture<TestDatabase>
{
    private async Task<(Project Project, FeatureFlag Flag)> GivenProjectWithFlag(string key = "flag")
    {
        var owner = await Seed.UserAsync(db);
        var project = await Seed.ProjectAsync(db, owner, false, "dev", "prod");
        var flag = await Seed.FlagAsync(db, project, key);
        return (project, flag);
    }

    #region Feature flags

    [Fact]
    public async Task Flag_lookups_include_expected_navigations()
    {
        var (project, flag) = await GivenProjectWithFlag();
        var dev = project.Scopes.Single(s => s.Alias == "dev");
        await using var ctx = db.CreateContext();
        var repo = new FeatureFlagRepository(ctx);

        Assert.NotNull(await repo.GetByIdAsync(flag.Id));
        Assert.Equal(2, (await repo.GetByIdWithValuesAsync(flag.Id))!.Values.Count);
        Assert.Equal(2, (await repo.GetByIdWithScopesAndProjectAsync(flag.Id))!.Project.Scopes.Count);
        Assert.Equal(flag.Id, (await repo.GetByProjectAndKeyAsync(project.Id, "flag"))!.Id);
        Assert.Single(await repo.GetByProjectIdAsync(project.Id));
        Assert.True(await repo.ExistsByIdAsync(flag.Id));
        Assert.True(await repo.ExistsByProjectAndKeyAsync(project.Id, "flag"));
        Assert.False(await repo.ExistsByProjectAndKeyExcludingIdAsync(project.Id, "flag", flag.Id));

        var value = (await repo.GetValueByFlagIdAndScopeIdAsync(flag.Id, dev.Id))!;
        var withNav = (await repo.GetValueByIdWithNavigationsAsync(value.Id))!;
        Assert.Equal(project.Alias, withNav.FeatureFlag.Project.Alias);
        Assert.Equal("dev", withNav.Scope.Alias);

        Assert.NotNull(await repo.GetByProjectScopeFlagAliasAsync(project.Alias, "dev", "flag"));
        var sdkValue = (await repo.GetByProjectIdScopeFlagKeyAsync(project.Id, "prod", "flag"))!;
        Assert.Equal("prod", sdkValue.Scope.Alias);
        Assert.Equal("flag", sdkValue.FeatureFlag.Key);
        Assert.Null(await repo.GetByProjectIdScopeFlagKeyAsync(project.Id, "prod", "missing"));

        var all = await repo.GetAllByProjectIdAndScopeAliasAsync(project.Id, "dev");
        Assert.Equal("flag", Assert.Single(all).FeatureFlag.Key);
    }

    [Fact]
    public async Task GetPaged_filters_case_insensitively_and_pages()
    {
        var owner = await Seed.UserAsync(db);
        var project = await Seed.ProjectAsync(db, owner);
        await Seed.FlagAsync(db, project, "checkout-v2", name: "Checkout");
        await Seed.FlagAsync(db, project, "search", name: "New CHECKOUT button");
        await Seed.FlagAsync(db, project, "banner", name: "Banner");
        await using var ctx = db.CreateContext();
        var repo = new FeatureFlagRepository(ctx);

        var (firstPage, total) = await repo.GetPagedAsync(project.Id, 0, 1, " checkout ");
        var (secondPage, _) = await repo.GetPagedAsync(project.Id, 1, 1, "checkout");
        var (everything, all) = await repo.GetPagedAsync(project.Id, 0, 10, null);

        Assert.Equal(2, total);
        Assert.Single(firstPage);
        Assert.NotEqual(firstPage[0].Id, Assert.Single(secondPage).Id);
        Assert.Equal(3, all);
        Assert.All(everything, f => Assert.NotEmpty(f.Values));
    }

    [Fact]
    public async Task Flag_add_update_values_and_delete()
    {
        var (project, flag) = await GivenProjectWithFlag("mutable");
        var dev = project.Scopes.Single(s => s.Alias == "dev");

        await using (var ctx = db.CreateContext())
        {
            var repo = new FeatureFlagRepository(ctx);
            var loaded = (await repo.GetByIdAsync(flag.Id))!;
            loaded.Name = "Renamed";
            await repo.UpdateAsync(loaded);

            var value = (await repo.GetValueByFlagIdAndScopeIdAsync(flag.Id, dev.Id))!;
            value.SetBooleanDefault(true);
            await repo.UpdateValueAsync(value);

            var extraScope = new Scope { Id = Guid.NewGuid(), ProjectId = project.Id, Alias = "qa", Name = "QA", CreatedAt = DateTime.UtcNow };
            ctx.Scopes.Add(extraScope);
            await ctx.SaveChangesAsync();
            await repo.AddValuesAsync([loaded.CreateValueForScope(extraScope.Id)]);

            var newFlag = new FeatureFlag { Id = Guid.NewGuid(), ProjectId = project.Id, Key = "added", Name = "Added", Type = FeatureFlagType.String, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            await repo.AddAsync(newFlag);
        }

        await using (var ctx = db.CreateContext())
        {
            var repo = new FeatureFlagRepository(ctx);
            var loaded = (await repo.GetByIdWithValuesAsync(flag.Id))!;
            Assert.Equal("Renamed", loaded.Name);
            Assert.Equal(3, loaded.Values.Count);
            Assert.True(loaded.Values.Single(v => v.ScopeId == dev.Id).IsEnabled);
            Assert.True(await repo.ExistsByProjectAndKeyAsync(project.Id, "added"));

            await repo.DeleteAsync(flag.Id);
            await repo.DeleteAsync(Guid.NewGuid());
        }

        await using (var ctx = db.CreateContext())
            Assert.False(await new FeatureFlagRepository(ctx).ExistsByIdAsync(flag.Id));
    }

    #endregion

    #region Targeting rules

    [Fact]
    public async Task Targeting_rules_crud_and_ordering()
    {
        var (project, flag) = await GivenProjectWithFlag("targeted");
        var dev = project.Scopes.Single(s => s.Alias == "dev");
        Guid valueId;
        await using (var ctx = db.CreateContext())
            valueId = (await new FeatureFlagRepository(ctx).GetValueByFlagIdAndScopeIdAsync(flag.Id, dev.Id))!.Id;

        var condition = new TargetingCondition { Id = Guid.NewGuid(), AttributeKey = "country", Operator = ComparisonOperator.Equals, Value = "US" };
        var second = TargetingRule.ForBoolean(valueId, 2, false);
        var first = TargetingRule.ForBoolean(valueId, 1, true, [condition]);

        await using (var ctx = db.CreateContext())
        {
            var repo = new TargetingRuleRepository(ctx);
            await repo.AddAsync(second);
            await repo.AddAsync(first);
        }

        await using (var ctx = db.CreateContext())
        {
            var repo = new TargetingRuleRepository(ctx);
            var rules = await repo.GetByFlagValueIdAsync(valueId);
            Assert.Equal([first.Id, second.Id], rules.Select(r => r.Id));
            Assert.Equal(2, await repo.CountByFlagValueIdAsync(valueId));
            Assert.True(await repo.PriorityExistsAsync(valueId, 2));
            Assert.False(await repo.PriorityExistsAsync(valueId, 2, excludeRuleId: second.Id));

            var loaded = (await repo.GetByIdWithConditionsAsync(first.Id))!;
            Assert.Equal(project.Alias, loaded.FeatureFlagValue.FeatureFlag.Project.Alias);
            Assert.Equal("dev", loaded.FeatureFlagValue.Scope.Alias);
            Assert.Single(loaded.Conditions);

            loaded.SetBoolean(false);
            await repo.UpdateAsync(loaded);
            foreach (var r in rules)
                r.Priority += 10;
            await repo.UpdateRangeAsync(rules);
        }

        await using (var ctx = db.CreateContext())
        {
            var repo = new TargetingRuleRepository(ctx);
            var loadedCondition = (await repo.GetConditionByIdAsync(condition.Id))!;
            Assert.Equal(first.Id, loadedCondition.TargetingRule.Id);
            Assert.Equal(project.Alias, loadedCondition.TargetingRule.FeatureFlagValue.FeatureFlag.Project.Alias);

            loadedCondition.Value = "CA";
            await repo.UpdateConditionAsync(loadedCondition);
            var extra = new TargetingCondition { Id = Guid.NewGuid(), TargetingRuleId = first.Id, AttributeKey = "plan", Operator = ComparisonOperator.In, Value = "[\"pro\"]" };
            await repo.AddConditionAsync(extra);
            await repo.DeleteConditionAsync(condition.Id);
            await repo.DeleteConditionAsync(Guid.NewGuid());
        }

        await using (var ctx = db.CreateContext())
        {
            var repo = new TargetingRuleRepository(ctx);
            var loaded = (await repo.GetByIdWithConditionsAsync(first.Id))!;
            Assert.False(loaded.ServeBooleanValue);
            Assert.Equal(11, loaded.Priority);
            Assert.Equal("plan", Assert.Single(loaded.Conditions).AttributeKey);

            await repo.DeleteAsync(first.Id);
            await repo.DeleteAsync(Guid.NewGuid());
            Assert.Equal(1, await repo.CountByFlagValueIdAsync(valueId));
        }
    }

    #endregion

    #region Segments

    [Fact]
    public async Task Segments_and_members()
    {
        var owner = await Seed.UserAsync(db);
        var project = await Seed.ProjectAsync(db, owner);
        var beta = new Segment { Id = Guid.NewGuid(), ProjectId = project.Id, Name = "beta", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var alpha = new Segment { Id = Guid.NewGuid(), ProjectId = project.Id, Name = "alpha", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

        await using (var ctx = db.CreateContext())
        {
            var repo = new SegmentRepository(ctx);
            await repo.AddAsync(beta);
            await repo.AddAsync(alpha);
            await repo.AddMembersAsync(new[] { "User-A", "user-b", "other" }.Select(k =>
                new SegmentMember { Id = Guid.NewGuid(), SegmentId = beta.Id, TargetingKey = k }));
        }

        await using (var ctx = db.CreateContext())
        {
            var repo = new SegmentRepository(ctx);
            Assert.Equal(["alpha", "beta"], (await repo.GetByProjectIdAsync(project.Id)).Select(s => s.Name));
            Assert.NotNull(await repo.GetByIdAsync(beta.Id));
            var withMembers = (await repo.GetByIdWithMembersAsync(beta.Id))!;
            Assert.Equal(3, withMembers.Members.Count);
            Assert.Equal(project.Alias, withMembers.Project.Alias);

            Assert.True(await repo.ExistsByProjectAndNameAsync(project.Id, "beta"));
            Assert.False(await repo.ExistsByProjectAndNameAsync(project.Id, "beta", excludeSegmentId: beta.Id));

            Assert.Equal(3, (await repo.GetMembersBySegmentIdAsync(beta.Id)).Count);
            var (page, total) = await repo.GetMembersPagedAsync(beta.Id, 0, 1, " USER ");
            Assert.Equal(2, total);
            Assert.Single(page);
            var (all, allTotal) = await repo.GetMembersPagedAsync(beta.Id, 0, 10, null);
            Assert.Equal(3, allTotal);
            Assert.Equal(3, all.Count);

            Assert.True(await repo.MemberExistsAsync(beta.Id, "User-A"));
            Assert.True(await repo.IsTargetingKeyInSegmentAsync(beta.Id, "user-b"));
            Assert.False(await repo.IsTargetingKeyInSegmentAsync(beta.Id, "user-a"));

            await repo.DeleteMemberByKeyAsync(beta.Id, "other");
            await repo.DeleteMemberByKeyAsync(beta.Id, "missing");
            withMembers.Name = "beta-2";
            await repo.UpdateAsync(withMembers);
        }

        await using (var ctx = db.CreateContext())
        {
            var repo = new SegmentRepository(ctx);
            Assert.Equal("beta-2", (await repo.GetByIdAsync(beta.Id))!.Name);
            Assert.Equal(2, (await repo.GetMembersBySegmentIdAsync(beta.Id)).Count);
            await repo.DeleteAsync(alpha.Id);
            await repo.DeleteAsync(Guid.NewGuid());
            Assert.Null(await repo.GetByIdAsync(alpha.Id));
        }
    }

    #endregion
}
