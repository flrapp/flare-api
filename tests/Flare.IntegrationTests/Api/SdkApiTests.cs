using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Flare.Application.DTOs;
using Flare.Application.DTOs.Sdk;
using Flare.Domain.Enums;
using Flare.IntegrationTests.TestSupport;

namespace Flare.IntegrationTests.Api;

public class SdkApiTests(FlareApiFactory factory) : IClassFixture<FlareApiFactory>
{
    private sealed record SdkProject(Guid ProjectId, string ApiKey, FeatureFlagResponseDto Flag);

    /// Admin creates a project with one boolean flag enabled in "dev" and a targeting rule
    /// that serves false to targeting key "blocked".
    private async Task<SdkProject> GivenProjectWithFlagAsync()
    {
        var admin = await factory.LoginAsAdminAsync();
        var alias = $"sdk-{Guid.NewGuid():N}"[..16];
        await (await admin.PostAsJsonAsync("/api/v1/projects", new CreateProjectDto { Alias = alias, Name = "SDK project" }))
            .EnsureStatusAsync(HttpStatusCode.Created);
        var project = (await admin.GetJsonAsync<List<ProjectResponseDto>>("/api/v1/projects")).Single(p => p.Alias == alias);

        await (await admin.PostAsJsonAsync($"/api/v1/projects/{project.Id}/feature-flags",
            new CreateFeatureFlagDto { Key = "new-checkout", Name = "New checkout", Type = FeatureFlagType.Boolean }))
            .EnsureStatusAsync(HttpStatusCode.Created);
        var flag = Assert.Single((await admin.GetJsonAsync<PagedResult<FeatureFlagResponseDto>>($"/api/v1/projects/{project.Id}/feature-flags")).Items);
        var dev = flag.Values.Single(v => v.ScopeAlias == "dev");

        await (await admin.PutAsJsonAsync($"/api/v1/feature-flags/{flag.Id}/values",
            new UpdateFeatureFlagValueDto { ScopeId = dev.ScopeId, Type = FeatureFlagType.Boolean, BooleanValue = true }))
            .EnsureStatusAsync(HttpStatusCode.OK);
        await (await admin.PostAsJsonAsync($"/api/v1/feature-flag-values/{dev.Id}/targeting-rules", new CreateTargetingRuleDto
        {
            ServeValue = new TypedValueDto { Type = FeatureFlagType.Boolean, Bool = false },
            Conditions = [new CreateTargetingConditionDto { AttributeKey = "targetingKey", Operator = ComparisonOperator.Equals, Value = "blocked" }]
        })).EnsureStatusAsync(HttpStatusCode.Created);

        var apiKey = (await admin.GetJsonAsync<ProjectApiKeyResponseDto>($"/api/v1/projects/{project.Id}/api-key")).ApiKey;
        return new SdkProject(project.Id, apiKey, flag);
    }

    private HttpClient SdkClient(string? apiKey)
    {
        var client = factory.CreateHttpsClient();
        if (apiKey is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        return client;
    }

    private static FlagEvaluationRequestDto Request(string flagKey = "new-checkout", string scope = "dev", string? targetingKey = null) =>
        new() { FlagKey = flagKey, Context = new EvaluationContextDto { Scope = scope, TargetingKey = targetingKey } };

    [Fact]
    public async Task Evaluate_returns_static_default_value()
    {
        var sdk = await GivenProjectWithFlagAsync();

        var response = await SdkClient(sdk.ApiKey).PostAsJsonAsync("/sdk/v1/flags/evaluate", Request(targetingKey: "regular"));

        await response.EnsureStatusAsync(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("new-checkout", body.GetProperty("flagKey").GetString());
        Assert.True(body.GetProperty("value").GetBoolean());
        Assert.Equal("STATIC", body.GetProperty("reason").GetString());
        Assert.Equal("boolean", body.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Evaluate_applies_targeting_rules()
    {
        var sdk = await GivenProjectWithFlagAsync();

        var response = await SdkClient(sdk.ApiKey).PostAsJsonAsync("/sdk/v1/flags/evaluate", Request(targetingKey: "blocked"));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("value").GetBoolean());
        Assert.Equal("TARGETING_MATCH", body.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Evaluate_in_other_scope_uses_that_scopes_default()
    {
        var sdk = await GivenProjectWithFlagAsync();

        var response = await SdkClient(sdk.ApiKey).PostAsJsonAsync("/sdk/v1/flags/evaluate", Request(scope: "production"));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("value").GetBoolean());
    }

    [Fact]
    public async Task EvaluateAll_returns_every_flag_in_scope()
    {
        var sdk = await GivenProjectWithFlagAsync();

        var response = await SdkClient(sdk.ApiKey).PostAsJsonAsync("/sdk/v1/flags/evaluate-all",
            new BulkEvaluationRequestDto { Context = new EvaluationContextDto { Scope = "dev", TargetingKey = "blocked" } });

        await response.EnsureStatusAsync(HttpStatusCode.OK);
        var flags = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("flags");
        var flag = Assert.Single(flags.EnumerateArray());
        Assert.Equal("TARGETING_MATCH", flag.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Evaluate_unknown_scope_or_flag_returns_404()
    {
        var sdk = await GivenProjectWithFlagAsync();
        var client = SdkClient(sdk.ApiKey);

        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/sdk/v1/flags/evaluate", Request(scope: "nope"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/sdk/v1/flags/evaluate", Request(flagKey: "nope"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/sdk/v1/flags/evaluate-all",
            new BulkEvaluationRequestDto { Context = new EvaluationContextDto { Scope = "nope" } })).StatusCode);
    }

    [Fact]
    public async Task Evaluate_with_invalid_payload_returns_400()
    {
        var sdk = await GivenProjectWithFlagAsync();

        var response = await SdkClient(sdk.ApiKey).PostAsJsonAsync("/sdk/v1/flags/evaluate", new { flagKey = "new-checkout" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Evaluate_rejects_missing_or_invalid_api_key()
    {
        var missing = await SdkClient(null).PostAsJsonAsync("/sdk/v1/flags/evaluate", Request());
        var invalid = await SdkClient("not-a-real-key").PostAsJsonAsync("/sdk/v1/flags/evaluate", Request());
        var basicClient = factory.CreateHttpsClient();
        basicClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", "abc");
        var wrongScheme = await basicClient.PostAsJsonAsync("/sdk/v1/flags/evaluate", Request());

        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongScheme.StatusCode);
    }

    [Fact]
    public async Task Regenerated_key_invalidates_old_key()
    {
        var sdk = await GivenProjectWithFlagAsync();
        var admin = await factory.LoginAsAdminAsync();

        await (await admin.PostAsync($"/api/v1/projects/{sdk.ProjectId}/regenerate-api-key", null)).EnsureStatusAsync(HttpStatusCode.OK);

        Assert.Equal(HttpStatusCode.Unauthorized, (await SdkClient(sdk.ApiKey).PostAsJsonAsync("/sdk/v1/flags/evaluate", Request())).StatusCode);
    }

    [Fact]
    public async Task Operational_endpoints_are_available()
    {
        var client = factory.CreateHttpsClient();

        await (await client.GetAsync("/health/live")).EnsureStatusAsync(HttpStatusCode.OK);
        await (await client.GetAsync("/health/ready")).EnsureStatusAsync(HttpStatusCode.OK);
        await (await client.GetAsync("/metrics")).EnsureStatusAsync(HttpStatusCode.OK);
        await (await client.GetAsync("/openapi/v1.json")).EnsureStatusAsync(HttpStatusCode.OK);
    }
}

/// Separate host with a tiny SDK budget so the rate limiter can be exercised deterministically.
public sealed class RateLimitedApiFactory : FlareApiFactory
{
    protected override int SdkPermitsPerSecond => 2;
}

public class SdkRateLimitingTests(RateLimitedApiFactory factory) : IClassFixture<RateLimitedApiFactory>
{
    [Fact]
    public async Task Sdk_requests_over_budget_get_429()
    {
        var client = factory.CreateHttpsClient();
        var request = new FlagEvaluationRequestDto { FlagKey = "any", Context = new EvaluationContextDto { Scope = "dev" } };

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 6; i++)
            statuses.Add((await client.PostAsJsonAsync("/sdk/v1/flags/evaluate", request)).StatusCode);

        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
    }

    [Fact]
    public async Task Web_ui_endpoints_are_not_rate_limited()
    {
        var client = factory.CreateHttpsClient();

        for (var i = 0; i < 6; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
    }
}
