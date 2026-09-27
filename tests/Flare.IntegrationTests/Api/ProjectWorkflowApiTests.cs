using System.Net;
using System.Net.Http.Json;
using Flare.Application.DTOs;
using Flare.Domain.Enums;
using Flare.IntegrationTests.TestSupport;

namespace Flare.IntegrationTests.Api;

public class ProjectWorkflowApiTests(FlareApiFactory factory) : IClassFixture<FlareApiFactory>
{
    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..16];

    private static async Task<ProjectResponseDto> CreateProjectAsync(HttpClient client)
    {
        var alias = Unique("proj");
        var response = await client.PostAsJsonAsync("/api/v1/projects", new CreateProjectDto { Alias = alias, Name = "Test project" });
        await response.EnsureStatusAsync(HttpStatusCode.Created);
        var projects = await client.GetJsonAsync<List<ProjectResponseDto>>("/api/v1/projects");
        return projects.Single(p => p.Alias == alias);
    }

    private static async Task<FeatureFlagResponseDto> CreateFlagAsync(HttpClient client, Guid projectId, FeatureFlagType type = FeatureFlagType.Boolean)
    {
        var key = Unique("flag");
        var response = await client.PostAsJsonAsync($"/api/v1/projects/{projectId}/feature-flags",
            new CreateFeatureFlagDto { Key = key, Name = "Test flag", Type = type });
        await response.EnsureStatusAsync(HttpStatusCode.Created);
        var page = await client.GetJsonAsync<PagedResult<FeatureFlagResponseDto>>($"/api/v1/projects/{projectId}/feature-flags?search={key}");
        return Assert.Single(page.Items);
    }

    #region Projects

    [Fact]
    public async Task Project_lifecycle()
    {
        var admin = await factory.LoginAsAdminAsync();
        var project = await CreateProjectAsync(admin);
        var url = $"/api/v1/projects/{project.Id}";

        var detail = await admin.GetJsonAsync<ProjectDetailResponseDto>(url);
        Assert.Equal(project.Alias, detail.Alias);

        await (await admin.PutAsJsonAsync(url, new UpdateProjectDto { Alias = project.Alias, Name = "Renamed project", Description = "d" }))
            .EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal("Renamed project", (await admin.GetJsonAsync<ProjectDetailResponseDto>(url)).Name);

        var key = (await admin.GetJsonAsync<ProjectApiKeyResponseDto>($"{url}/api-key")).ApiKey;
        await (await admin.PostAsync($"{url}/regenerate-api-key", null)).EnsureStatusAsync(HttpStatusCode.OK);
        Assert.NotEqual(key, (await admin.GetJsonAsync<ProjectApiKeyResponseDto>($"{url}/api-key")).ApiKey);

        await (await admin.PostAsync($"{url}/archive", null)).EnsureStatusAsync(HttpStatusCode.NoContent);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync($"{url}/archive", null)).StatusCode);
        await (await admin.PostAsync($"{url}/unarchive", null)).EnsureStatusAsync(HttpStatusCode.NoContent);

        var permissions = await admin.GetJsonAsync<MyPermissionsResponseDto>($"{url}/my-permissions");
        Assert.Equal(Enum.GetValues<ProjectPermission>().Length, permissions.ProjectPermissions.Count);
        Assert.Equal(3, permissions.ScopePermissions.Count);

        await (await admin.DeleteAsync(url)).EnsureStatusAsync(HttpStatusCode.NoContent);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Project_endpoints_validate_input_and_uniqueness()
    {
        var admin = await factory.LoginAsAdminAsync();
        var project = await CreateProjectAsync(admin);

        var tooShort = await admin.PostAsJsonAsync("/api/v1/projects", new CreateProjectDto { Alias = Unique("x"), Name = "ab" });
        var duplicate = await admin.PostAsJsonAsync("/api/v1/projects", new CreateProjectDto { Alias = project.Alias, Name = "Another" });
        var badUpdate = await admin.PutAsJsonAsync($"/api/v1/projects/{project.Id}", new UpdateProjectDto { Alias = "a", Name = "x" });

        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badUpdate.StatusCode);
    }

    #endregion

    #region Scopes

    [Fact]
    public async Task Scope_lifecycle()
    {
        var admin = await factory.LoginAsAdminAsync();
        var project = await CreateProjectAsync(admin);
        var scopesUrl = $"/api/v1/projects/{project.Id}/scopes";

        await (await admin.PostAsJsonAsync(scopesUrl, new CreateScopeDto { Alias = "qa", Name = "QA" })).EnsureStatusAsync(HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(scopesUrl, new CreateScopeDto { Alias = "qa", Name = "QA" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(scopesUrl, new CreateScopeDto { Alias = "x", Name = "" })).StatusCode);

        var scopes = await admin.GetJsonAsync<List<ScopeResponseDto>>(scopesUrl);
        Assert.Equal(["dev", "staging", "production", "qa"], scopes.Select(s => s.Alias));
        var qa = scopes.Single(s => s.Alias == "qa");

        await (await admin.PutAsJsonAsync($"/api/v1/scopes/{qa.Id}", new UpdateScopeDto { Alias = "qa2", Name = "QA two" })).EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/v1/scopes/{qa.Id}", new UpdateScopeDto { Alias = "q", Name = "" })).StatusCode);
        await (await admin.DeleteAsync($"/api/v1/scopes/{qa.Id}")).EnsureStatusAsync(HttpStatusCode.NoContent);

        Assert.DoesNotContain(await admin.GetJsonAsync<List<ScopeResponseDto>>(scopesUrl), s => s.Id == qa.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/api/v1/scopes/{qa.Id}")).StatusCode);
    }

    #endregion

    #region Feature flags

    [Fact]
    public async Task Feature_flag_lifecycle()
    {
        var admin = await factory.LoginAsAdminAsync();
        var project = await CreateProjectAsync(admin);
        var flag = await CreateFlagAsync(admin, project.Id);
        Assert.Equal(3, flag.Values.Count);
        var dev = flag.Values.Single(v => v.ScopeAlias == "dev");
        Assert.False(dev.BooleanValue);

        var page = await admin.GetJsonAsync<PagedResult<FeatureFlagResponseDto>>($"/api/v1/projects/{project.Id}/feature-flags?page=-1&pageSize=0");
        Assert.Equal(1, page.Page);
        Assert.Equal(1, page.PageSize);
        Assert.Equal(1, page.TotalCount);

        var invalidFlag = await admin.PostAsJsonAsync($"/api/v1/projects/{project.Id}/feature-flags",
            new CreateFeatureFlagDto { Key = "x", Name = "a", Type = FeatureFlagType.Boolean });
        Assert.Equal(HttpStatusCode.BadRequest, invalidFlag.StatusCode);

        await (await admin.PutAsJsonAsync($"/api/v1/feature-flags/{flag.Id}/values",
            new UpdateFeatureFlagValueDto { ScopeId = dev.ScopeId, Type = FeatureFlagType.Boolean, BooleanValue = true }))
            .EnsureStatusAsync(HttpStatusCode.OK);
        await (await admin.PutAsJsonAsync($"/api/v1/feature-flags/{flag.Id}",
            new UpdateFeatureFlagDto { Key = flag.Key, Name = "Renamed flag", Description = "d" }))
            .EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/v1/feature-flags/{flag.Id}",
            new UpdateFeatureFlagDto { Key = "k", Name = "n" })).StatusCode);
        // [Required] on a non-nullable Guid never fails, so a missing scopeId surfaces as "Scope not found".
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsync($"/api/v1/feature-flags/{flag.Id}/values",
            JsonContent.Create(new { }))).StatusCode);

        var reloaded = await admin.GetJsonAsync<FeatureFlagResponseDto>($"/api/v1/feature-flags/{flag.Id}");
        Assert.Equal("Renamed flag", reloaded.Name);
        Assert.True(reloaded.Values.Single(v => v.ScopeAlias == "dev").BooleanValue);

        await (await admin.DeleteAsync($"/api/v1/feature-flags/{flag.Id}")).EnsureStatusAsync(HttpStatusCode.NoContent);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/feature-flags/{flag.Id}")).StatusCode);
    }

    [Fact]
    public async Task String_flag_value_update_round_trips()
    {
        var admin = await factory.LoginAsAdminAsync();
        var project = await CreateProjectAsync(admin);
        var flag = await CreateFlagAsync(admin, project.Id, FeatureFlagType.String);
        var dev = flag.Values.Single(v => v.ScopeAlias == "dev");

        await (await admin.PutAsJsonAsync($"/api/v1/feature-flags/{flag.Id}/values",
            new UpdateFeatureFlagValueDto { ScopeId = dev.ScopeId, Type = FeatureFlagType.String, StringValue = "blue" }))
            .EnsureStatusAsync(HttpStatusCode.OK);

        var reloaded = await admin.GetJsonAsync<FeatureFlagResponseDto>($"/api/v1/feature-flags/{flag.Id}");
        Assert.Equal("blue", reloaded.Values.Single(v => v.ScopeAlias == "dev").StringValue);
    }

    #endregion

    #region Targeting rules and conditions

    [Fact]
    public async Task Targeting_rule_and_condition_lifecycle()
    {
        var admin = await factory.LoginAsAdminAsync();
        var project = await CreateProjectAsync(admin);
        var flag = await CreateFlagAsync(admin, project.Id);
        var valueId = flag.Values.Single(v => v.ScopeAlias == "dev").Id;
        var rulesUrl = $"/api/v1/feature-flag-values/{valueId}/targeting-rules";

        CreateTargetingRuleDto Rule(string country) => new()
        {
            ServeValue = new TypedValueDto { Type = FeatureFlagType.Boolean, Bool = true },
            Conditions = [new CreateTargetingConditionDto { AttributeKey = "country", Operator = ComparisonOperator.Equals, Value = country }]
        };

        await (await admin.PostAsJsonAsync(rulesUrl, Rule("US"))).EnsureStatusAsync(HttpStatusCode.Created);
        await (await admin.PostAsJsonAsync(rulesUrl, Rule("CA"))).EnsureStatusAsync(HttpStatusCode.Created);
        var invalid = await admin.PostAsJsonAsync(rulesUrl, new CreateTargetingRuleDto
        {
            ServeValue = new TypedValueDto { Type = FeatureFlagType.Boolean, Bool = true },
            Conditions = [new CreateTargetingConditionDto { AttributeKey = "age", Operator = ComparisonOperator.GreaterThan, Value = "old" }]
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var mismatched = await admin.PostAsJsonAsync(rulesUrl, new CreateTargetingRuleDto
        {
            ServeValue = new TypedValueDto { Type = FeatureFlagType.String, String = "x" },
            Conditions = [new CreateTargetingConditionDto { AttributeKey = "a", Operator = ComparisonOperator.Equals, Value = "b" }]
        });
        Assert.Equal(HttpStatusCode.BadRequest, mismatched.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(rulesUrl, new { })).StatusCode);

        var rules = await admin.GetJsonAsync<List<TargetingRuleDto>>(rulesUrl);
        Assert.Equal([1, 2], rules.Select(r => r.Priority));
        var (us, ca) = (rules[0], rules[1]);

        await (await admin.PutAsJsonAsync($"{rulesUrl}/reorder", new ReorderTargetingRulesDto { RuleIds = [ca.Id, us.Id] }))
            .EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"{rulesUrl}/reorder", new { ruleIds = Array.Empty<Guid>() })).StatusCode);
        await (await admin.PutAsJsonAsync($"/api/v1/targeting-rules/{us.Id}", new UpdateTargetingRuleDto
        {
            Priority = 2,
            ServeValue = new TypedValueDto { Type = FeatureFlagType.Boolean, Bool = false }
        })).EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/v1/targeting-rules/{us.Id}", new { })).StatusCode);

        await (await admin.PostAsJsonAsync($"/api/v1/targeting-rules/{us.Id}/conditions",
            new CreateTargetingConditionDto { AttributeKey = "plan", Operator = ComparisonOperator.In, Value = "[\"pro\"]" }))
            .EnsureStatusAsync(HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/v1/targeting-rules/{us.Id}/conditions", new { })).StatusCode);

        rules = await admin.GetJsonAsync<List<TargetingRuleDto>>(rulesUrl);
        var updatedUs = rules.Single(r => r.Id == us.Id);
        Assert.Equal(2, updatedUs.Priority);
        Assert.Equal(2, updatedUs.Conditions.Count);
        var planCondition = updatedUs.Conditions.Single(c => c.AttributeKey == "plan");

        await (await admin.PutAsJsonAsync($"/api/v1/targeting-conditions/{planCondition.Id}",
            new UpdateTargetingConditionDto { AttributeKey = "plan", Operator = ComparisonOperator.NotIn, Value = "[\"free\"]" }))
            .EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/v1/targeting-conditions/{planCondition.Id}", new { })).StatusCode);
        await (await admin.DeleteAsync($"/api/v1/targeting-conditions/{planCondition.Id}")).EnsureStatusAsync(HttpStatusCode.NoContent);
        var lastCondition = updatedUs.Conditions.Single(c => c.AttributeKey == "country");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync($"/api/v1/targeting-conditions/{lastCondition.Id}")).StatusCode);

        await (await admin.DeleteAsync($"/api/v1/targeting-rules/{ca.Id}")).EnsureStatusAsync(HttpStatusCode.NoContent);
        var remaining = Assert.Single(await admin.GetJsonAsync<List<TargetingRuleDto>>(rulesUrl));
        Assert.Equal(1, remaining.Priority);
    }

    #endregion

    #region Segments

    [Fact]
    public async Task Segment_and_member_lifecycle()
    {
        var admin = await factory.LoginAsAdminAsync();
        var project = await CreateProjectAsync(admin);
        var segmentsUrl = $"/api/v1/projects/{project.Id}/segments";

        await (await admin.PostAsJsonAsync(segmentsUrl, new CreateSegmentDto { Name = "beta", Description = "testers" })).EnsureStatusAsync(HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(segmentsUrl, new CreateSegmentDto { Name = "beta" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(segmentsUrl, new { description = "no name" })).StatusCode);
        var segment = Assert.Single(await admin.GetJsonAsync<List<SegmentResponseDto>>(segmentsUrl));
        var membersUrl = $"/api/v1/segments/{segment.Id}/members";

        await (await admin.PostAsJsonAsync(membersUrl, new AddSegmentMembersDto { TargetingKeys = ["user-1", "user-2", "user-1", "vip"] }))
            .EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(membersUrl, new { targetingKeys = Array.Empty<string>() })).StatusCode);

        var members = await admin.GetJsonAsync<PagedResult<SegmentMemberResponseDto>>($"{membersUrl}?search=user&page=1&pageSize=1");
        Assert.Equal(2, members.TotalCount);
        Assert.Single(members.Items);
        Assert.Equal(3, Assert.Single(await admin.GetJsonAsync<List<SegmentResponseDto>>(segmentsUrl)).MemberCount);

        await (await admin.DeleteAsync($"{membersUrl}/vip")).EnsureStatusAsync(HttpStatusCode.NoContent);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"{membersUrl}/vip")).StatusCode);

        await (await admin.PutAsJsonAsync($"{segmentsUrl}/{segment.Id}", new UpdateSegmentDto { Name = "beta-testers" })).EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"{segmentsUrl}/{segment.Id}", new { })).StatusCode);
        Assert.Equal("beta-testers", Assert.Single(await admin.GetJsonAsync<List<SegmentResponseDto>>(segmentsUrl)).Name);

        await (await admin.DeleteAsync($"{segmentsUrl}/{segment.Id}")).EnsureStatusAsync(HttpStatusCode.NoContent);
        Assert.Empty(await admin.GetJsonAsync<List<SegmentResponseDto>>(segmentsUrl));
    }

    #endregion

    #region Project members and permissions

    [Fact]
    public async Task Members_are_invited_scoped_and_removed()
    {
        var admin = await factory.LoginAsAdminAsync();
        var project = await CreateProjectAsync(admin);
        var username = Unique("member").Replace('-', '_');
        var (memberClient, member) = await factory.CreateUserAndLoginAsync(admin, username);
        var usersUrl = $"/api/v1/projects/{project.Id}/users";

        Assert.Equal(HttpStatusCode.Forbidden, (await memberClient.GetAsync($"/api/v1/projects/{project.Id}/scopes")).StatusCode);

        var available = await admin.GetJsonAsync<List<AvailableUserDto>>($"{usersUrl}/available?search={username}");
        Assert.False(Assert.Single(available).IsAlreadyMember);

        var scopes = await admin.GetJsonAsync<List<ScopeResponseDto>>($"/api/v1/projects/{project.Id}/scopes");
        var dev = scopes.Single(s => s.Alias == "dev");
        var invite = await admin.PostAsJsonAsync(usersUrl, new InviteUserDto
        {
            UserId = member.UserId,
            ProjectPermissions = [ProjectPermission.ManageFeatureFlags],
            ScopePermissions = new() { [dev.Id] = [ScopePermission.ReadFeatureFlags] }
        });
        await invite.EnsureStatusAsync(HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(usersUrl, new InviteUserDto { UserId = member.UserId })).StatusCode);

        var myPermissions = await memberClient.GetJsonAsync<MyPermissionsResponseDto>($"/api/v1/projects/{project.Id}/my-permissions");
        Assert.Equal([ProjectPermission.ManageFeatureFlags], myPermissions.ProjectPermissions);
        Assert.Equal(3, (await memberClient.GetJsonAsync<List<ScopeResponseDto>>($"/api/v1/projects/{project.Id}/scopes")).Count);
        Assert.Contains(await memberClient.GetJsonAsync<List<ProjectResponseDto>>("/api/v1/projects"), p => p.Id == project.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await memberClient.PostAsJsonAsync($"/api/v1/projects/{project.Id}/scopes",
            new CreateScopeDto { Alias = "nope", Name = "Nope" })).StatusCode);

        var list = await admin.GetJsonAsync<PagedResult<ProjectUserResponseDto>>($"{usersUrl}?search={username}&page=0&pageSize=500");
        Assert.Equal(member.UserId, Assert.Single(list.Items).UserId);
        Assert.Equal(100, list.PageSize);

        var update = await admin.PutAsJsonAsync($"{usersUrl}/{member.UserId}/permissions", new UpdateUserPermissionsDto
        {
            ProjectPermissions = [ProjectPermission.ManageScopes],
            ScopePermissions = new() { [dev.Id] = [ScopePermission.ReadFeatureFlags, ScopePermission.UpdateFeatureFlags] }
        });
        await update.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal([ProjectPermission.ManageScopes], (await update.Content.ReadFromJsonAsync<ProjectUserResponseDto>())!.ProjectPermissions);
        await (await memberClient.PostAsJsonAsync($"/api/v1/projects/{project.Id}/scopes", new CreateScopeDto { Alias = "member-qa", Name = "Member QA" }))
            .EnsureStatusAsync(HttpStatusCode.Created);

        await (await admin.DeleteAsync($"{usersUrl}/{member.UserId}")).EnsureStatusAsync(HttpStatusCode.NoContent);
        Assert.Equal(HttpStatusCode.Forbidden, (await memberClient.GetAsync($"/api/v1/projects/{project.Id}/scopes")).StatusCode);
    }

    [Fact]
    public async Task Last_project_owner_cannot_be_removed()
    {
        var admin = await factory.LoginAsAdminAsync();
        var me = await admin.GetJsonAsync<AuthResultDto>("/api/v1/auth/me");
        var project = await CreateProjectAsync(admin);

        var response = await admin.DeleteAsync($"/api/v1/projects/{project.Id}/users/{me.UserId}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Non_members_are_forbidden_from_project_resources()
    {
        var admin = await factory.LoginAsAdminAsync();
        var project = await CreateProjectAsync(admin);
        var flag = await CreateFlagAsync(admin, project.Id);
        var (outsider, _) = await factory.CreateUserAndLoginAsync(admin, Unique("out").Replace('-', '_'));

        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync($"/api/v1/projects/{project.Id}/feature-flags")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync($"/api/v1/feature-flags/{flag.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync($"/api/v1/projects/{project.Id}/my-permissions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync($"/api/v1/projects/{project.Id}/segments")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync($"/api/v1/projects/{project.Id}/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.DeleteAsync($"/api/v1/projects/{project.Id}")).StatusCode);
    }

    #endregion
}
