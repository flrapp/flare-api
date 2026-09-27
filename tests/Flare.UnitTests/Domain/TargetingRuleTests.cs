using Flare.Domain.Entities;
using Flare.Domain.Enums;
using Flare.Domain.Exceptions;

namespace Flare.UnitTests.Domain;

public class TargetingRuleTests
{
    private static readonly Guid FlagValueId = Guid.NewGuid();

    [Fact]
    public void ForBoolean_sets_serve_value_priority_and_conditions()
    {
        var condition = new TargetingCondition { AttributeKey = "country", Operator = ComparisonOperator.Equals, Value = "US" };

        var rule = TargetingRule.ForBoolean(FlagValueId, 3, true, [condition]);

        Assert.NotEqual(Guid.Empty, rule.Id);
        Assert.Equal(FlagValueId, rule.FeatureFlagValueId);
        Assert.Equal(3, rule.Priority);
        Assert.True(rule.ServeBooleanValue);
        Assert.Single(rule.Conditions);
    }

    [Fact]
    public void Factories_default_to_empty_conditions()
    {
        Assert.Empty(TargetingRule.ForString(FlagValueId, 1, "x").Conditions);
        Assert.Equal(1.5, TargetingRule.ForNumber(FlagValueId, 1, 1.5).ServeNumberValue);
        Assert.Equal("[1,2]", TargetingRule.ForJson(FlagValueId, 1, "[1,2]").ServeJsonValue);
    }

    [Fact]
    public void ForString_with_null_throws_validation_exception()
    {
        var ex = Assert.Throws<ValidationException>(() => TargetingRule.ForString(FlagValueId, 1, null!));

        Assert.Contains("serveValue.string", ex.Errors.Keys);
    }

    [Fact]
    public void ForJson_with_invalid_json_throws_validation_exception()
    {
        var ex = Assert.Throws<ValidationException>(() => TargetingRule.ForJson(FlagValueId, 1, "{not json"));

        Assert.Contains("serveValue.json", ex.Errors.Keys);
    }

    [Fact]
    public void ForJson_with_null_throws_validation_exception()
    {
        Assert.Throws<ValidationException>(() => TargetingRule.ForJson(FlagValueId, 1, null!));
    }

    [Fact]
    public void Setters_replace_serve_value_and_clear_others()
    {
        var rule = TargetingRule.ForBoolean(FlagValueId, 1, true);

        rule.SetString("a");
        Assert.Null(rule.ServeBooleanValue);
        Assert.Equal("a", rule.ServeStringValue);

        rule.SetNumber(7);
        Assert.Null(rule.ServeStringValue);
        Assert.Equal(7, rule.ServeNumberValue);

        rule.SetJson("{}");
        Assert.Null(rule.ServeNumberValue);
        Assert.Equal("{}", rule.ServeJsonValue);

        rule.SetBoolean(false);
        Assert.Null(rule.ServeJsonValue);
        Assert.False(rule.ServeBooleanValue);
    }

    [Fact]
    public void SetJson_with_invalid_json_throws()
    {
        var rule = TargetingRule.ForJson(FlagValueId, 1, "{}");

        Assert.Throws<ValidationException>(() => rule.SetJson("oops"));
    }

    [Fact]
    public void SetString_with_null_throws()
    {
        var rule = TargetingRule.ForString(FlagValueId, 1, "a");

        Assert.Throws<ValidationException>(() => rule.SetString(null!));
    }
}
