using System.Text.Json;
using Flare.Application.DTOs;
using Flare.Domain.Entities;
using Flare.Domain.Enums;
using Flare.Domain.Exceptions;

namespace Flare.UnitTests.Application;

public class DtoBehaviorTests
{
    private static readonly Guid FlagValueId = Guid.NewGuid();

    public static TheoryData<TypedValueDto, FeatureFlagType> ValidServeValues => new()
    {
        { new TypedValueDto { Type = FeatureFlagType.Boolean, Bool = true }, FeatureFlagType.Boolean },
        { new TypedValueDto { Type = FeatureFlagType.String, String = "s" }, FeatureFlagType.String },
        { new TypedValueDto { Type = FeatureFlagType.Number, Number = 1.5 }, FeatureFlagType.Number },
        { new TypedValueDto { Type = FeatureFlagType.Json, Json = "{}" }, FeatureFlagType.Json }
    };

    [Theory]
    [MemberData(nameof(ValidServeValues))]
    public void TypedValue_BuildRule_creates_rule_for_flag_type(TypedValueDto dto, FeatureFlagType type)
    {
        var rule = dto.BuildRule(2, FlagValueId, type, []);

        Assert.Equal(2, rule.Priority);
        Assert.Equal(FlagValueId, rule.FeatureFlagValueId);
        switch (type)
        {
            case FeatureFlagType.Boolean: Assert.True(rule.ServeBooleanValue); break;
            case FeatureFlagType.String: Assert.Equal("s", rule.ServeStringValue); break;
            case FeatureFlagType.Number: Assert.Equal(1.5, rule.ServeNumberValue); break;
            case FeatureFlagType.Json: Assert.Equal("{}", rule.ServeJsonValue); break;
        }
    }

    [Theory]
    [MemberData(nameof(ValidServeValues))]
    public void TypedValue_ApplyTo_replaces_serve_value(TypedValueDto dto, FeatureFlagType type)
    {
        var rule = TargetingRule.ForNumber(FlagValueId, 1, 0);

        dto.ApplyTo(rule, type);

        if (type != FeatureFlagType.Number)
            Assert.Null(rule.ServeNumberValue);
    }

    [Fact]
    public void TypedValue_type_mismatch_throws_validation()
    {
        var dto = new TypedValueDto { Type = FeatureFlagType.String, String = "s" };

        var ex = Assert.Throws<ValidationException>(() => dto.BuildRule(1, FlagValueId, FeatureFlagType.Boolean, []));
        Assert.Contains("serveValue", ex.Errors.Keys);
        Assert.Throws<ValidationException>(() => dto.ApplyTo(TargetingRule.ForBoolean(FlagValueId, 1, true), FeatureFlagType.Boolean));
    }

    [Theory]
    [InlineData(FeatureFlagType.Boolean, "serveValue.bool")]
    [InlineData(FeatureFlagType.String, "serveValue.string")]
    [InlineData(FeatureFlagType.Number, "serveValue.number")]
    [InlineData(FeatureFlagType.Json, "serveValue.json")]
    public void TypedValue_missing_value_throws_validation(FeatureFlagType type, string expectedKey)
    {
        var dto = new TypedValueDto { Type = type };

        var ex = Assert.Throws<ValidationException>(() => dto.BuildRule(1, FlagValueId, type, []));

        Assert.Contains(expectedKey, ex.Errors.Keys);
    }

    [Fact]
    public void TypedValue_unknown_type_throws_validation()
    {
        var dto = new TypedValueDto { Type = (FeatureFlagType)9 };

        Assert.Throws<ValidationException>(() => dto.BuildRule(1, FlagValueId, (FeatureFlagType)9, []));
    }

    [Fact]
    public void TypedValue_ApplyTo_unknown_type_is_a_no_op()
    {
        var rule = TargetingRule.ForBoolean(FlagValueId, 1, true);

        new TypedValueDto { Type = (FeatureFlagType)9 }.ApplyTo(rule, (FeatureFlagType)9);

        Assert.True(rule.ServeBooleanValue);
    }

    private static FeatureFlagValue EmptyValue() => FeatureFlagValue.ForString(Guid.NewGuid(), Guid.NewGuid(), "old");

    [Fact]
    public void UpdateValue_ApplyDefault_boolean_defaults_to_false_when_missing()
    {
        var value = EmptyValue();
        value.SetBooleanDefault(true);

        new UpdateFeatureFlagValueDto { Type = FeatureFlagType.Boolean }.ApplyDefault(value);

        Assert.False(value.IsEnabled);
    }

    [Fact]
    public void UpdateValue_ApplyDefault_sets_typed_columns()
    {
        var value = EmptyValue();

        new UpdateFeatureFlagValueDto { Type = FeatureFlagType.String, StringValue = "new" }.ApplyDefault(value);
        Assert.Equal("new", value.DefaultStringValue);

        new UpdateFeatureFlagValueDto { Type = FeatureFlagType.Number, NumberValue = 3 }.ApplyDefault(value);
        Assert.Equal(3, value.DefaultNumberValue);
        Assert.Null(value.DefaultStringValue);

        using var doc = JsonDocument.Parse("{\"a\": [1, 2]}");
        new UpdateFeatureFlagValueDto { Type = FeatureFlagType.Json, JsonValue = doc.RootElement.Clone() }.ApplyDefault(value);
        Assert.Equal("{\"a\": [1, 2]}", value.DefaultJsonValue);

        new UpdateFeatureFlagValueDto { Type = FeatureFlagType.Json }.ApplyDefault(value);
        Assert.Null(value.DefaultJsonValue);
    }

    [Fact]
    public void UpdateValue_ApplyDefault_unknown_type_throws_validation()
    {
        var ex = Assert.Throws<ValidationException>(() =>
            new UpdateFeatureFlagValueDto { Type = (FeatureFlagType)7 }.ApplyDefault(EmptyValue()));

        Assert.Contains("type", ex.Errors.Keys);
    }

    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(10, 10, 1)]
    [InlineData(11, 10, 2)]
    [InlineData(5, 0, 0)]
    public void PagedResult_TotalPages_rounds_up(int total, int pageSize, int expectedPages)
    {
        var result = new PagedResult<int> { TotalCount = total, PageSize = pageSize };

        Assert.Equal(expectedPages, result.TotalPages);
    }
}
