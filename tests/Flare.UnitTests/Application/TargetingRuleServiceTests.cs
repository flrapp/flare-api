using Flare.Application.Audit;
using Flare.Application.DTOs;
using Flare.Application.Interfaces;
using Flare.Application.Services;
using Flare.Domain.Entities;
using Flare.Domain.Enums;
using Flare.Domain.Exceptions;
using Flare.Infrastructure.Data.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Hybrid;
using NSubstitute;

namespace Flare.UnitTests.Application;

public class TargetingRuleServiceTests
{
    private readonly ITargetingRuleRepository _rules = Substitute.For<ITargetingRuleRepository>();
    private readonly IFeatureFlagRepository _flags = Substitute.For<IFeatureFlagRepository>();
    private readonly IPermissionService _permissions = Substitute.For<IPermissionService>();
    private readonly HybridCache _cache = Substitute.For<HybridCache>();
    private readonly IAuditLogger _audit = Substitute.For<IAuditLogger>();
    private readonly TargetingRuleService _sut;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly FeatureFlagValue _flagValue;

    public TargetingRuleServiceTests()
    {
        _sut = new TargetingRuleService(_rules, _flags, _permissions, _cache, _audit);

        var project = new Project { Id = Guid.NewGuid(), Alias = "proj" };
        var flag = new FeatureFlag { Id = Guid.NewGuid(), ProjectId = project.Id, Project = project, Key = "flag", Type = FeatureFlagType.Boolean };
        var scope = new Scope { Id = Guid.NewGuid(), Alias = "dev" };
        _flagValue = flag.CreateValueForScope(scope.Id);
        _flagValue.FeatureFlag = flag;
        _flagValue.Scope = scope;
        _flags.GetValueByIdWithNavigationsAsync(_flagValue.Id).Returns(_flagValue);
    }

    private void GrantManageRules() =>
        _permissions.HasProjectPermissionAsync(_userId, _flagValue.FeatureFlag.ProjectId, ProjectPermission.ManageTargetingRules).Returns(true);

    private TargetingRule GivenRule(int priority = 1, params TargetingCondition[] conditions)
    {
        var rule = TargetingRule.ForBoolean(_flagValue.Id, priority, true, conditions);
        rule.FeatureFlagValue = _flagValue;
        foreach (var c in rule.Conditions)
            c.TargetingRule = rule;
        _rules.GetByIdWithConditionsAsync(rule.Id).Returns(rule);
        return rule;
    }

    private static TargetingCondition Condition(string value = "x", ComparisonOperator op = ComparisonOperator.Equals) =>
        new() { Id = Guid.NewGuid(), AttributeKey = "attr", Operator = op, Value = value };

    private static CreateTargetingRuleDto CreateDto(ComparisonOperator op = ComparisonOperator.Equals, string value = "x") => new()
    {
        ServeValue = new TypedValueDto { Type = FeatureFlagType.Boolean, Bool = true },
        Conditions = [new CreateTargetingConditionDto { AttributeKey = "attr", Operator = op, Value = value }]
    };

    private async Task CacheWasInvalidated() =>
        await _cache.Received().RemoveAsync("feature_proj_dev_flag", Arg.Any<CancellationToken>());

    [Fact]
    public async Task GetRules_maps_rules_with_serve_values()
    {
        GrantManageRules();
        var rule = GivenRule(1, Condition());
        _rules.GetByFlagValueIdAsync(_flagValue.Id).Returns([rule]);

        var result = await _sut.GetRulesAsync(_flagValue.Id, _userId);

        var dto = Assert.Single(result);
        Assert.Equal(rule.Id, dto.Id);
        Assert.Equal(1, dto.Priority);
        Assert.Equal("true", System.Text.Json.JsonSerializer.Serialize(dto.ServeValue));
        Assert.Equal("attr", Assert.Single(dto.Conditions).AttributeKey);
    }

    [Fact]
    public async Task CreateRule_appends_with_next_priority_and_invalidates_cache()
    {
        GrantManageRules();
        _rules.CountByFlagValueIdAsync(_flagValue.Id).Returns(2);
        TargetingRule? added = null;
        await _rules.AddAsync(Arg.Do<TargetingRule>(r => added = r));

        await _sut.CreateRuleAsync(_flagValue.Id, CreateDto(), _userId, "alice");

        Assert.NotNull(added);
        Assert.Equal(3, added.Priority);
        Assert.True(added.ServeBooleanValue);
        Assert.Single(added.Conditions);
        await CacheWasInvalidated();
        _audit.Received(1).LogProjectAudit("proj", "alice", "TargetingRule", "dev", "Created");
    }

    [Theory]
    [InlineData(ComparisonOperator.In, "not-json")]
    [InlineData(ComparisonOperator.NotIn, "{\"a\":1}")]
    [InlineData(ComparisonOperator.GreaterThan, "ten")]
    [InlineData(ComparisonOperator.LessThan, "")]
    [InlineData(ComparisonOperator.InSegment, "segment-name")]
    [InlineData(ComparisonOperator.NotInSegment, "123")]
    public async Task CreateRule_rejects_invalid_condition_values(ComparisonOperator op, string value)
    {
        GrantManageRules();

        await Assert.ThrowsAsync<BadRequestException>(() => _sut.CreateRuleAsync(_flagValue.Id, CreateDto(op, value), _userId, "alice"));

        await _rules.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    [Theory]
    [InlineData(ComparisonOperator.In, "[\"a\"]")]
    [InlineData(ComparisonOperator.GreaterThan, "1.5")]
    [InlineData(ComparisonOperator.InSegment, "0f8fad5b-d9cb-469f-a165-70867728950e")]
    public async Task CreateRule_accepts_valid_condition_values(ComparisonOperator op, string value)
    {
        GrantManageRules();

        await _sut.CreateRuleAsync(_flagValue.Id, CreateDto(op, value), _userId, "alice");

        await _rules.ReceivedWithAnyArgs(1).AddAsync(default!);
    }

    [Fact]
    public async Task CreateRule_with_mismatched_serve_type_throws_validation()
    {
        GrantManageRules();
        var dto = new CreateTargetingRuleDto
        {
            ServeValue = new TypedValueDto { Type = FeatureFlagType.String, String = "x" },
            Conditions = [new CreateTargetingConditionDto { AttributeKey = "a", Operator = ComparisonOperator.Equals, Value = "b" }]
        };

        await Assert.ThrowsAsync<ValidationException>(() => _sut.CreateRuleAsync(_flagValue.Id, dto, _userId, "alice"));
    }

    [Fact]
    public async Task UpdateRule_changes_priority_and_serve_value()
    {
        GrantManageRules();
        var rule = GivenRule(1, Condition());

        await _sut.UpdateRuleAsync(rule.Id, new UpdateTargetingRuleDto
        {
            Priority = 2,
            ServeValue = new TypedValueDto { Type = FeatureFlagType.Boolean, Bool = false }
        }, _userId, "alice");

        Assert.Equal(2, rule.Priority);
        Assert.False(rule.ServeBooleanValue);
        await _rules.Received(1).UpdateAsync(rule);
        await CacheWasInvalidated();
    }

    [Fact]
    public async Task UpdateRule_rejects_taken_priority()
    {
        GrantManageRules();
        var rule = GivenRule(1);
        _rules.PriorityExistsAsync(_flagValue.Id, 2, rule.Id).Returns(true);

        await Assert.ThrowsAsync<BadRequestException>(() => _sut.UpdateRuleAsync(rule.Id, new UpdateTargetingRuleDto
        {
            Priority = 2,
            ServeValue = new TypedValueDto { Type = FeatureFlagType.Boolean, Bool = false }
        }, _userId, "alice"));
    }

    [Fact]
    public async Task DeleteRule_compacts_following_priorities()
    {
        GrantManageRules();
        var deleted = GivenRule(2);
        var before = TargetingRule.ForBoolean(_flagValue.Id, 1, true);
        var after = TargetingRule.ForBoolean(_flagValue.Id, 3, true);
        _rules.GetByFlagValueIdAsync(_flagValue.Id).Returns([before, after]);

        await _sut.DeleteRuleAsync(deleted.Id, _userId, "alice");

        await _rules.Received(1).DeleteAsync(deleted.Id);
        Assert.Equal(1, before.Priority);
        Assert.Equal(2, after.Priority);
        await _rules.Received(1).UpdateRangeAsync(Arg.Is<IEnumerable<TargetingRule>>(r => r.Single() == after));
        await CacheWasInvalidated();
    }

    [Fact]
    public async Task DeleteRule_last_rule_skips_compaction()
    {
        GrantManageRules();
        var rule = GivenRule(1);
        _rules.GetByFlagValueIdAsync(_flagValue.Id).Returns([]);

        await _sut.DeleteRuleAsync(rule.Id, _userId, "alice");

        await _rules.DidNotReceiveWithAnyArgs().UpdateRangeAsync(default!);
    }

    [Fact]
    public async Task ReorderRules_assigns_priorities_in_requested_order()
    {
        GrantManageRules();
        var first = TargetingRule.ForBoolean(_flagValue.Id, 1, true);
        var second = TargetingRule.ForBoolean(_flagValue.Id, 2, true);
        _rules.GetByFlagValueIdAsync(_flagValue.Id).Returns([first, second]);

        await _sut.ReorderRulesAsync(_flagValue.Id, new ReorderTargetingRulesDto { RuleIds = [second.Id, first.Id] }, _userId, "alice");

        Assert.Equal(1, second.Priority);
        Assert.Equal(2, first.Priority);
        await CacheWasInvalidated();
        _audit.Received(1).LogProjectAudit("proj", "alice", "TargetingRule", "dev", "Reordered");
    }

    [Fact]
    public async Task ReorderRules_requires_exact_rule_set()
    {
        GrantManageRules();
        var rule = TargetingRule.ForBoolean(_flagValue.Id, 1, true);
        _rules.GetByFlagValueIdAsync(_flagValue.Id).Returns([rule]);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _sut.ReorderRulesAsync(_flagValue.Id, new ReorderTargetingRulesDto { RuleIds = [Guid.NewGuid()] }, _userId, "alice"));
    }

    [Fact]
    public async Task AddCondition_adds_validated_condition_to_rule()
    {
        GrantManageRules();
        var rule = GivenRule(1, Condition());

        await _sut.AddConditionAsync(rule.Id, new CreateTargetingConditionDto
        {
            AttributeKey = "age",
            Operator = ComparisonOperator.GreaterThan,
            Value = "18"
        }, _userId, "alice");

        await _rules.Received(1).AddConditionAsync(Arg.Is<TargetingCondition>(c => c.TargetingRuleId == rule.Id && c.AttributeKey == "age"));
        Assert.Equal(2, rule.Conditions.Count);
        await CacheWasInvalidated();
    }

    [Fact]
    public async Task UpdateCondition_changes_fields()
    {
        GrantManageRules();
        var condition = Condition();
        GivenRule(1, condition);
        _rules.GetConditionByIdAsync(condition.Id).Returns(condition);

        await _sut.UpdateConditionAsync(condition.Id, new UpdateTargetingConditionDto
        {
            AttributeKey = "country",
            Operator = ComparisonOperator.In,
            Value = "[\"US\"]"
        }, _userId, "alice");

        Assert.Equal("country", condition.AttributeKey);
        Assert.Equal(ComparisonOperator.In, condition.Operator);
        await _rules.Received(1).UpdateConditionAsync(condition);
        await CacheWasInvalidated();
    }

    [Fact]
    public async Task DeleteCondition_removes_when_rule_keeps_another()
    {
        GrantManageRules();
        var condition = Condition();
        GivenRule(1, condition, Condition("y"));
        _rules.GetConditionByIdAsync(condition.Id).Returns(condition);

        await _sut.DeleteConditionAsync(condition.Id, _userId, "alice");

        await _rules.Received(1).DeleteConditionAsync(condition.Id);
        await CacheWasInvalidated();
    }

    [Fact]
    public async Task DeleteCondition_refuses_to_remove_last_condition()
    {
        GrantManageRules();
        var condition = Condition();
        GivenRule(1, condition);
        _rules.GetConditionByIdAsync(condition.Id).Returns(condition);

        await Assert.ThrowsAsync<BadRequestException>(() => _sut.DeleteConditionAsync(condition.Id, _userId, "alice"));
    }

    public static TheoryData<string> Operations =>
        new() { "get", "create", "update", "delete", "reorder", "addCondition", "updateCondition", "deleteCondition" };

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task Operations_on_missing_entities_throw_not_found(string operation)
    {
        GrantManageRules();

        await Assert.ThrowsAsync<NotFoundException>(Invoke(operation, Guid.NewGuid()));
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task Operations_without_permission_are_forbidden(string operation)
    {
        var condition = Condition();
        var rule = GivenRule(1, condition, Condition());
        _rules.GetConditionByIdAsync(condition.Id).Returns(condition);

        var id = operation switch
        {
            "get" or "create" or "reorder" => _flagValue.Id,
            "updateCondition" or "deleteCondition" => condition.Id,
            _ => rule.Id
        };

        await Assert.ThrowsAsync<ForbiddenException>(Invoke(operation, id));
    }

    private Func<Task> Invoke(string operation, Guid id) => operation switch
    {
        "get" => () => _sut.GetRulesAsync(id, _userId),
        "create" => () => _sut.CreateRuleAsync(id, CreateDto(), _userId, "alice"),
        "update" => () => _sut.UpdateRuleAsync(id, new UpdateTargetingRuleDto
        {
            Priority = 1,
            ServeValue = new TypedValueDto { Type = FeatureFlagType.Boolean, Bool = true }
        }, _userId, "alice"),
        "delete" => () => _sut.DeleteRuleAsync(id, _userId, "alice"),
        "reorder" => () => _sut.ReorderRulesAsync(id, new ReorderTargetingRulesDto { RuleIds = [] }, _userId, "alice"),
        "addCondition" => () => _sut.AddConditionAsync(id, new CreateTargetingConditionDto { AttributeKey = "a", Value = "b" }, _userId, "alice"),
        "updateCondition" => () => _sut.UpdateConditionAsync(id, new UpdateTargetingConditionDto { AttributeKey = "a", Value = "b" }, _userId, "alice"),
        _ => () => _sut.DeleteConditionAsync(id, _userId, "alice")
    };
}
