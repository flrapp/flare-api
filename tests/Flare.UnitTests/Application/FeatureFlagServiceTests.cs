using System.Text.Json;
using Flare.Application.Audit;
using Flare.Application.DTOs;
using Flare.Application.DTOs.Sdk;
using Flare.Application.Interfaces;
using Flare.Application.Services;
using Flare.Domain.Entities;
using Flare.Domain.Enums;
using Flare.Domain.Exceptions;
using Flare.Infrastructure.Data.Repositories.Interfaces;
using Flare.UnitTests.TestSupport;
using Microsoft.Extensions.Caching.Hybrid;
using NSubstitute;

namespace Flare.UnitTests.Application;

public class FeatureFlagServiceTests
{
    private readonly IFeatureFlagRepository _flags = Substitute.For<IFeatureFlagRepository>();
    private readonly IProjectRepository _projects = Substitute.For<IProjectRepository>();
    private readonly IScopeRepository _scopes = Substitute.For<IScopeRepository>();
    private readonly ISegmentRepository _segments = Substitute.For<ISegmentRepository>();
    private readonly IPermissionService _permissions = Substitute.For<IPermissionService>();
    private readonly HybridCache _cache = Substitute.For<HybridCache>();
    private readonly IAuditLogger _audit = Substitute.For<IAuditLogger>();

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Project _project;
    private readonly Scope _dev;

    public FeatureFlagServiceTests()
    {
        _project = new Project { Id = Guid.NewGuid(), Alias = "proj" };
        _dev = new Scope { Id = Guid.NewGuid(), ProjectId = _project.Id, Project = _project, Alias = "dev", Name = "Dev" };
        _project.Scopes.Add(_dev);
        _projects.GetByIdAsync(_project.Id).Returns(_project);
        _scopes.GetByProjectAndAliasAsync(_project.Id, "dev").Returns(_dev);
    }

    private FeatureFlagService CreateSut(HybridCache? cache = null) =>
        new(_flags, _projects, _scopes, _segments, _permissions, cache ?? _cache, _audit);

    private FeatureFlag GivenFlag(FeatureFlagType type = FeatureFlagType.Boolean, string key = "flag")
    {
        var flag = new FeatureFlag { Id = Guid.NewGuid(), ProjectId = _project.Id, Project = _project, Key = key, Name = "Flag", Type = type };
        _flags.GetByIdWithScopesAndProjectAsync(flag.Id).Returns(flag);
        return flag;
    }

    private FeatureFlagValue ValueFor(FeatureFlag flag)
    {
        var value = flag.CreateValueForScope(_dev.Id);
        value.FeatureFlag = flag;
        value.Scope = _dev;
        flag.Values.Add(value);
        return value;
    }

    #region CRUD

    [Fact]
    public async Task Create_adds_flag_with_value_per_scope()
    {
        _permissions.HasProjectPermissionAsync(_userId, _project.Id, ProjectPermission.ManageFeatureFlags).Returns(true);
        var prod = new Scope { Id = Guid.NewGuid() };
        _scopes.GetByProjectIdAsync(_project.Id).Returns([_dev, prod]);
        FeatureFlag? added = null;
        await _flags.AddAsync(Arg.Do<FeatureFlag>(f => added = f));

        await CreateSut().CreateAsync(_project.Id,
            new CreateFeatureFlagDto { Key = "new-flag", Name = "New", Type = FeatureFlagType.Number }, _userId, "alice");

        Assert.NotNull(added);
        Assert.Equal(FeatureFlagType.Number, added.Type);
        Assert.Equal([_dev.Id, prod.Id], added.Values.Select(v => v.ScopeId));
        _audit.Received(1).LogProjectAudit("proj", "alice", "FeatureFlag", null, "Created");
    }

    [Fact]
    public async Task Create_rejects_duplicate_key()
    {
        _permissions.HasProjectPermissionAsync(_userId, _project.Id, ProjectPermission.ManageFeatureFlags).Returns(true);
        _flags.ExistsByProjectAndKeyAsync(_project.Id, "dup").Returns(true);

        await Assert.ThrowsAsync<ConflictException>(() => CreateSut().CreateAsync(_project.Id,
            new CreateFeatureFlagDto { Key = "dup", Name = "Dup", Type = FeatureFlagType.Boolean }, _userId, "alice"));
    }

    [Fact]
    public async Task Create_requires_permission_and_project()
    {
        var dto = new CreateFeatureFlagDto { Key = "k", Name = "Name", Type = FeatureFlagType.Boolean };
        await Assert.ThrowsAsync<ForbiddenException>(() => CreateSut().CreateAsync(_project.Id, dto, _userId, "alice"));

        var missing = Guid.NewGuid();
        _permissions.HasProjectPermissionAsync(_userId, missing, ProjectPermission.ManageFeatureFlags).Returns(true);
        await Assert.ThrowsAsync<NotFoundException>(() => CreateSut().CreateAsync(missing, dto, _userId, "alice"));
    }

    [Fact]
    public async Task Update_renames_and_invalidates_previous_key_tag()
    {
        var flag = GivenFlag(key: "old-key");
        _permissions.HasProjectPermissionAsync(_userId, _project.Id, ProjectPermission.ManageFeatureFlags).Returns(true);

        await CreateSut().UpdateAsync(flag.Id, new UpdateFeatureFlagDto { Key = "new-key", Name = "Renamed", Description = "d" }, _userId, "alice");

        Assert.Equal("new-key", flag.Key);
        Assert.Equal("Renamed", flag.Name);
        await _flags.Received(1).UpdateAsync(flag);
        await _cache.Received(1).RemoveByTagAsync("tag_proj_old-key", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_removes_flag_and_invalidates_tag()
    {
        var flag = GivenFlag();
        _permissions.HasProjectPermissionAsync(_userId, _project.Id, ProjectPermission.ManageFeatureFlags).Returns(true);

        await CreateSut().DeleteAsync(flag.Id, _userId, "alice");

        await _flags.Received(1).DeleteAsync(flag.Id);
        await _cache.Received(1).RemoveByTagAsync("tag_proj_flag", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_and_delete_require_existing_flag_and_permission()
    {
        var dto = new UpdateFeatureFlagDto { Key = "k", Name = "Name" };
        await Assert.ThrowsAsync<NotFoundException>(() => CreateSut().UpdateAsync(Guid.NewGuid(), dto, _userId, "alice"));
        await Assert.ThrowsAsync<NotFoundException>(() => CreateSut().DeleteAsync(Guid.NewGuid(), _userId, "alice"));

        var flag = GivenFlag();
        await Assert.ThrowsAsync<ForbiddenException>(() => CreateSut().UpdateAsync(flag.Id, dto, _userId, "alice"));
        await Assert.ThrowsAsync<ForbiddenException>(() => CreateSut().DeleteAsync(flag.Id, _userId, "alice"));
    }

    [Fact]
    public async Task GetByProjectId_maps_each_flag_with_values()
    {
        _permissions.IsProjectMemberAsync(_userId, _project.Id).Returns(true);
        var flag = GivenFlag(FeatureFlagType.Json);
        var value = ValueFor(flag);
        value.SetJsonDefault("{\"x\":1}");
        var missing = new FeatureFlag { Id = Guid.NewGuid() };
        _flags.GetByProjectIdAsync(_project.Id).Returns([flag, missing]);
        _flags.GetByIdWithValuesAsync(flag.Id).Returns(flag);

        var result = await CreateSut().GetByProjectIdAsync(_project.Id, _userId);

        var dto = Assert.Single(result);
        var valueDto = Assert.Single(dto.Values);
        Assert.Equal("dev", valueDto.ScopeAlias);
        Assert.Null(valueDto.BooleanValue);
        Assert.Equal(1, valueDto.JsonValue!.Value.GetProperty("x").GetInt32());
    }

    [Fact]
    public async Task GetPagedByProjectId_reloads_flags_without_loaded_values()
    {
        _permissions.IsProjectMemberAsync(_userId, _project.Id).Returns(true);
        var bare = GivenFlag();
        var loaded = GivenFlag();
        ValueFor(loaded);
        _flags.GetPagedAsync(_project.Id, 20, 10, "q").Returns(([bare], 21));
        _flags.GetByIdWithValuesAsync(bare.Id).Returns(loaded);

        var result = await CreateSut().GetPagedByProjectIdAsync(_project.Id, _userId, page: 3, pageSize: 10, search: "q");

        Assert.Equal(21, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        var value = Assert.Single(Assert.Single(result.Items).Values);
        Assert.False(value.BooleanValue);
    }

    [Fact]
    public async Task GetPagedByProjectId_throws_when_reload_finds_nothing()
    {
        _permissions.IsProjectMemberAsync(_userId, _project.Id).Returns(true);
        var flag = GivenFlag();
        _flags.GetPagedAsync(_project.Id, 0, 10, null).Returns(([flag], 1));

        await Assert.ThrowsAsync<NotFoundException>(() => CreateSut().GetPagedByProjectIdAsync(_project.Id, _userId, 1, 10, null));
    }

    [Fact]
    public async Task Reads_require_membership()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => CreateSut().GetByProjectIdAsync(_project.Id, _userId));
        await Assert.ThrowsAsync<ForbiddenException>(() => CreateSut().GetPagedByProjectIdAsync(_project.Id, _userId, 1, 10, null));

        var flag = GivenFlag();
        _flags.GetByIdWithValuesAsync(flag.Id).Returns(flag);
        await Assert.ThrowsAsync<ForbiddenException>(() => CreateSut().GetByIdAsync(flag.Id, _userId));
    }

    [Fact]
    public async Task GetById_returns_mapped_flag_or_throws()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => CreateSut().GetByIdAsync(Guid.NewGuid(), _userId));

        var flag = GivenFlag(FeatureFlagType.String);
        ValueFor(flag).SetStringDefault("hello");
        _flags.GetByIdWithValuesAsync(flag.Id).Returns(flag);
        _permissions.IsProjectMemberAsync(_userId, _project.Id).Returns(true);

        var result = await CreateSut().GetByIdAsync(flag.Id, _userId);

        Assert.Equal("hello", Assert.Single(result.Values).StringValue);
    }

    #endregion

    #region UpdateValue

    private UpdateFeatureFlagValueDto BoolUpdate(bool value) =>
        new() { ScopeId = _dev.Id, Type = FeatureFlagType.Boolean, BooleanValue = value };

    [Fact]
    public async Task UpdateValue_applies_default_and_invalidates_flag_cache_key()
    {
        var flag = GivenFlag();
        var value = ValueFor(flag);
        _flags.GetValueByFlagIdAndScopeIdAsync(flag.Id, _dev.Id).Returns(value);
        _permissions.HasScopePermissionAsync(_userId, _dev.Id, ScopePermission.UpdateFeatureFlags).Returns(true);

        await CreateSut().UpdateValueAsync(flag.Id, BoolUpdate(true), _userId, "alice");

        Assert.True(value.IsEnabled);
        await _flags.Received(1).UpdateValueAsync(value);
        await _cache.Received(1).RemoveAsync("feature_proj_dev_flag", Arg.Any<CancellationToken>());
        _audit.Received(1).LogProjectAudit("proj", "alice", "FeatureFlag", "dev", "ValueUpdated", Arg.Any<object>(), Arg.Any<object>());
    }

    [Fact]
    public async Task UpdateValue_creates_missing_value_for_scope()
    {
        var flag = GivenFlag();
        _permissions.HasScopePermissionAsync(_userId, _dev.Id, ScopePermission.UpdateFeatureFlags).Returns(true);
        FeatureFlagValue? added = null;
        await _flags.AddValuesAsync(Arg.Do<IEnumerable<FeatureFlagValue>>(v => added = v.Single()));

        await CreateSut().UpdateValueAsync(flag.Id, BoolUpdate(true), _userId, "alice");

        Assert.NotNull(added);
        Assert.Equal(_dev.Id, added.ScopeId);
        Assert.True(added.IsEnabled);
        await _flags.DidNotReceiveWithAnyArgs().UpdateValueAsync(default!);
        await _cache.Received(1).RemoveAsync("feature_proj_dev_flag", Arg.Any<CancellationToken>());
        _audit.Received(1).LogProjectAudit("proj", "alice", "FeatureFlag", "dev", "ValueUpdated", Arg.Any<object>(), Arg.Any<object>());
    }

    [Fact]
    public async Task UpdateValue_validation_paths()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => CreateSut().UpdateValueAsync(Guid.NewGuid(), BoolUpdate(true), _userId, "alice"));

        var flag = GivenFlag();
        await Assert.ThrowsAsync<ForbiddenException>(() => CreateSut().UpdateValueAsync(flag.Id, BoolUpdate(true), _userId, "alice"));

        _permissions.HasScopePermissionAsync(_userId, Arg.Any<Guid>(), ScopePermission.UpdateFeatureFlags).Returns(true);
        var wrongType = new UpdateFeatureFlagValueDto { ScopeId = _dev.Id, Type = FeatureFlagType.String, StringValue = "x" };
        await Assert.ThrowsAsync<BadRequestException>(() => CreateSut().UpdateValueAsync(flag.Id, wrongType, _userId, "alice"));

        var unknownScope = new UpdateFeatureFlagValueDto { ScopeId = Guid.NewGuid(), Type = FeatureFlagType.Boolean };
        await Assert.ThrowsAsync<NotFoundException>(() => CreateSut().UpdateValueAsync(flag.Id, unknownScope, _userId, "alice"));
    }

    [Fact]
    public async Task UpdateValue_rejects_scope_from_other_project()
    {
        var flag = GivenFlag();
        var foreign = new Scope { Id = Guid.NewGuid(), ProjectId = Guid.NewGuid(), Alias = "x" };
        _project.Scopes.Add(foreign);
        _permissions.HasScopePermissionAsync(_userId, foreign.Id, ScopePermission.UpdateFeatureFlags).Returns(true);

        await Assert.ThrowsAsync<BadRequestException>(() => CreateSut().UpdateValueAsync(flag.Id,
            new UpdateFeatureFlagValueDto { ScopeId = foreign.Id, Type = FeatureFlagType.Boolean }, _userId, "alice"));
    }

    #endregion

    #region Legacy value lookup

    [Fact]
    public async Task GetFeatureFlagValue_reads_through_cache()
    {
        var value = FeatureFlagValue.ForBoolean(Guid.NewGuid(), _dev.Id, true);
        _flags.GetByProjectScopeFlagAliasAsync("proj", "dev", "flag").Returns(value);
        var sut = CreateSut(Caches.CreateReal());

        var first = await sut.GetFeatureFlagValueAsync("proj", "dev", "flag");
        var second = await sut.GetFeatureFlagValueAsync("proj", "dev", "flag");

        Assert.True(first.Value);
        Assert.Equal(first, second);
        await _flags.Received(1).GetByProjectScopeFlagAliasAsync("proj", "dev", "flag");
    }

    [Fact]
    public async Task GetFeatureFlagValue_unknown_flag_throws_not_found()
    {
        var sut = CreateSut(Caches.CreateReal());

        await Assert.ThrowsAsync<NotFoundException>(() => sut.GetFeatureFlagValueAsync("proj", "dev", "missing"));
    }

    #endregion

    #region Evaluation

    private static EvaluationContextDto Context(string? targetingKey = null, Dictionary<string, string>? attributes = null) =>
        new() { Scope = "dev", TargetingKey = targetingKey, Attributes = attributes };

    private FeatureFlagValue GivenEvaluable(FeatureFlagType type, Action<FeatureFlagValue>? configure = null)
    {
        var flag = GivenFlag(type);
        var value = ValueFor(flag);
        configure?.Invoke(value);
        _flags.GetByProjectIdScopeFlagKeyAsync(_project.Id, "dev", "flag").Returns(value);
        return value;
    }

    private static TargetingRule AddRule(FeatureFlagValue value, int priority, bool serve, params TargetingCondition[] conditions)
    {
        var rule = TargetingRule.ForBoolean(value.Id, priority, serve, conditions);
        value.TargetingRules.Add(rule);
        return rule;
    }

    private static TargetingCondition When(string key, ComparisonOperator op, string conditionValue) =>
        new() { Id = Guid.NewGuid(), AttributeKey = key, Operator = op, Value = conditionValue };

    private async Task<FlagEvaluationResponseDto> Evaluate(EvaluationContextDto? context = null) =>
        await CreateSut().EvaluateFlagAsync(_project.Id, "flag", context ?? Context());

    private static string Json(object? value) => JsonSerializer.Serialize(value);

    [Fact]
    public async Task Evaluate_without_rules_returns_static_default()
    {
        GivenEvaluable(FeatureFlagType.String, v => v.SetStringDefault("blue"));

        var result = await Evaluate();

        Assert.Equal("flag", result.FlagKey);
        Assert.Equal("STATIC", result.Reason);
        Assert.Equal("\"blue\"", Json(result.Value));
        Assert.Equal(FeatureFlagType.String, result.Type);
        Assert.Null(result.Variant);
    }

    [Fact]
    public async Task Evaluate_first_matching_rule_by_priority_wins()
    {
        var value = GivenEvaluable(FeatureFlagType.Boolean);
        AddRule(value, 2, false, When("targetingKey", ComparisonOperator.Equals, "u1"));
        AddRule(value, 1, true, When("targetingKey", ComparisonOperator.StartsWith, "u"));

        var result = await Evaluate(Context("u1"));

        Assert.Equal("TARGETING_MATCH", result.Reason);
        Assert.Equal("true", Json(result.Value));
    }

    [Fact]
    public async Task Evaluate_with_no_matching_rule_falls_back_to_default()
    {
        var value = GivenEvaluable(FeatureFlagType.Boolean);
        AddRule(value, 1, true, When("targetingKey", ComparisonOperator.Equals, "someone-else"));

        var result = await Evaluate(Context("u1"));

        Assert.Equal("STATIC", result.Reason);
        Assert.Equal("false", Json(result.Value));
    }

    [Fact]
    public async Task Evaluate_rule_requires_all_conditions()
    {
        var value = GivenEvaluable(FeatureFlagType.Boolean);
        AddRule(value, 1, true,
            When("country", ComparisonOperator.Equals, "US"),
            When("plan", ComparisonOperator.Equals, "pro"));

        var partial = await Evaluate(Context(attributes: new() { ["country"] = "US", ["plan"] = "free" }));
        var full = await Evaluate(Context(attributes: new() { ["country"] = "US", ["plan"] = "pro" }));

        Assert.Equal("STATIC", partial.Reason);
        Assert.Equal("TARGETING_MATCH", full.Reason);
    }

    [Theory]
    [InlineData(ComparisonOperator.Equals, "abc", "abc", true)]
    [InlineData(ComparisonOperator.Equals, "abc", "ABC", false)]
    [InlineData(ComparisonOperator.NotEquals, "abc", "xyz", true)]
    [InlineData(ComparisonOperator.NotEquals, "abc", "abc", false)]
    [InlineData(ComparisonOperator.Contains, "hello world", "lo w", true)]
    [InlineData(ComparisonOperator.Contains, "hello", "xyz", false)]
    [InlineData(ComparisonOperator.StartsWith, "hello", "he", true)]
    [InlineData(ComparisonOperator.StartsWith, "hello", "lo", false)]
    [InlineData(ComparisonOperator.EndsWith, "hello", "lo", true)]
    [InlineData(ComparisonOperator.EndsWith, "hello", "he", false)]
    [InlineData(ComparisonOperator.In, "b", "[\"a\",\"b\"]", true)]
    [InlineData(ComparisonOperator.In, "c", "[\"a\",\"b\"]", false)]
    [InlineData(ComparisonOperator.In, "a", "not-json", false)]
    [InlineData(ComparisonOperator.NotIn, "c", "[\"a\",\"b\"]", true)]
    [InlineData(ComparisonOperator.NotIn, "a", "[\"a\",\"b\"]", false)]
    [InlineData(ComparisonOperator.NotIn, "a", "not-json", true)]
    [InlineData(ComparisonOperator.GreaterThan, "10.5", "10", true)]
    [InlineData(ComparisonOperator.GreaterThan, "9", "10", false)]
    [InlineData(ComparisonOperator.GreaterThan, "abc", "10", false)]
    [InlineData(ComparisonOperator.LessThan, "9", "10", true)]
    [InlineData(ComparisonOperator.LessThan, "10", "10", false)]
    [InlineData((ComparisonOperator)99, "x", "x", false)]
    public async Task Evaluate_operator_semantics(ComparisonOperator op, string attribute, string conditionValue, bool matches)
    {
        var value = GivenEvaluable(FeatureFlagType.Boolean);
        AddRule(value, 1, true, When("attr", op, conditionValue));

        var result = await Evaluate(Context(attributes: new() { ["attr"] = attribute }));

        Assert.Equal(matches ? "TARGETING_MATCH" : "STATIC", result.Reason);
    }

    [Fact]
    public async Task Evaluate_missing_attribute_never_matches()
    {
        var value = GivenEvaluable(FeatureFlagType.Boolean);
        AddRule(value, 1, true, When("attr", ComparisonOperator.NotEquals, "x"));

        var noAttributes = await Evaluate(Context());
        var otherAttributes = await Evaluate(Context(attributes: new() { ["other"] = "y" }));

        Assert.Equal("STATIC", noAttributes.Reason);
        Assert.Equal("STATIC", otherAttributes.Reason);
    }

    [Theory]
    [InlineData(ComparisonOperator.InSegment, true, true)]
    [InlineData(ComparisonOperator.InSegment, false, false)]
    [InlineData(ComparisonOperator.NotInSegment, true, false)]
    [InlineData(ComparisonOperator.NotInSegment, false, true)]
    public async Task Evaluate_segment_operators(ComparisonOperator op, bool isMember, bool matches)
    {
        var segmentId = Guid.NewGuid();
        _segments.IsTargetingKeyInSegmentAsync(segmentId, "u1").Returns(isMember);
        var value = GivenEvaluable(FeatureFlagType.Boolean);
        AddRule(value, 1, true, When("targetingKey", op, segmentId.ToString()));

        var result = await Evaluate(Context("u1"));

        Assert.Equal(matches ? "TARGETING_MATCH" : "STATIC", result.Reason);
    }

    [Fact]
    public async Task Evaluate_segment_condition_with_invalid_id_or_missing_key_does_not_match()
    {
        var value = GivenEvaluable(FeatureFlagType.Boolean);
        AddRule(value, 1, true, When("targetingKey", ComparisonOperator.InSegment, "not-a-guid"));
        AddRule(value, 2, true, When("targetingKey", ComparisonOperator.NotInSegment, Guid.NewGuid().ToString()));

        var result = await Evaluate(Context(targetingKey: null));

        Assert.Equal("STATIC", result.Reason);
        await _segments.DidNotReceiveWithAnyArgs().IsTargetingKeyInSegmentAsync(default, default!);
    }

    [Theory]
    [InlineData(FeatureFlagType.String, "\"served\"")]
    [InlineData(FeatureFlagType.Number, "42")]
    [InlineData(FeatureFlagType.Json, "{\"k\":\"v\"}")]
    public async Task Evaluate_serves_typed_rule_value(FeatureFlagType type, string expectedJson)
    {
        var value = GivenEvaluable(type);
        var condition = When("targetingKey", ComparisonOperator.Equals, "u1");
        value.TargetingRules.Add(type switch
        {
            FeatureFlagType.String => TargetingRule.ForString(value.Id, 1, "served", [condition]),
            FeatureFlagType.Number => TargetingRule.ForNumber(value.Id, 1, 42, [condition]),
            _ => TargetingRule.ForJson(value.Id, 1, "{\"k\":\"v\"}", [condition])
        });

        var result = await Evaluate(Context("u1"));

        Assert.Equal("TARGETING_MATCH", result.Reason);
        Assert.Equal(expectedJson, Json(result.Value));
    }

    [Fact]
    public async Task Evaluate_boolean_flag_reports_enabled_variant()
    {
        GivenEvaluable(FeatureFlagType.Boolean, v => v.SetBooleanDefault(true));

        var result = await Evaluate();

        Assert.Equal(true, result.Value);
        Assert.Equal("enabled", result.Variant);
    }

    [Fact]
    public async Task Evaluate_boolean_rule_match_reports_disabled_variant()
    {
        var value = GivenEvaluable(FeatureFlagType.Boolean, v => v.SetBooleanDefault(true));
        AddRule(value, 1, false, When("targetingKey", ComparisonOperator.Equals, "u1"));

        var result = await Evaluate(Context("u1"));

        Assert.Equal(false, result.Value);
        Assert.Equal("disabled", result.Variant);
    }

    [Fact]
    public async Task Evaluate_unknown_scope_or_flag_throws_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateSut().EvaluateFlagAsync(_project.Id, "flag", new EvaluationContextDto { Scope = "prod" }));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateSut().EvaluateFlagAsync(_project.Id, "missing", Context()));
    }

    [Fact]
    public async Task EvaluateAll_evaluates_every_flag_in_scope()
    {
        var on = GivenFlag(FeatureFlagType.Boolean, "on");
        var onValue = ValueFor(on);
        onValue.SetBooleanDefault(true);
        var targeted = GivenFlag(FeatureFlagType.Boolean, "targeted");
        var targetedValue = ValueFor(targeted);
        AddRule(targetedValue, 1, true, When("targetingKey", ComparisonOperator.Equals, "u1"));
        _flags.GetAllByProjectIdAndScopeAliasAsync(_project.Id, "dev").Returns([onValue, targetedValue]);

        var result = await CreateSut().EvaluateAllFlagsAsync(_project.Id, Context("u1"));

        Assert.Equal(["on", "targeted"], result.Flags.Select(f => f.FlagKey));
        Assert.Equal(["STATIC", "TARGETING_MATCH"], result.Flags.Select(f => f.Reason));
        Assert.All(result.Flags, f => Assert.Equal("true", Json(f.Value)));
    }

    [Fact]
    public async Task EvaluateAll_unknown_scope_throws_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateSut().EvaluateAllFlagsAsync(_project.Id, new EvaluationContextDto { Scope = "prod" }));
    }

    #endregion
}
