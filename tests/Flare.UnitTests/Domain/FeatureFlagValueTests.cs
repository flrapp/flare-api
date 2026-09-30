using System.Text.Json;
using Flare.Domain.Entities;
using Flare.Domain.Enums;

namespace Flare.UnitTests.Domain;

public class FeatureFlagValueTests
{
    private static readonly Guid FlagId = Guid.NewGuid();
    private static readonly Guid ScopeId = Guid.NewGuid();

    private static FeatureFlagValue WithFlag(FeatureFlagValue value, FeatureFlagType type)
    {
        value.FeatureFlag = new FeatureFlag { Id = FlagId, Type = type };
        return value;
    }

    [Fact]
    public void ForBoolean_sets_ids_and_enabled_state()
    {
        var value = FeatureFlagValue.ForBoolean(FlagId, ScopeId, true);

        Assert.NotEqual(Guid.Empty, value.Id);
        Assert.Equal(FlagId, value.FeatureFlagId);
        Assert.Equal(ScopeId, value.ScopeId);
        Assert.True(value.IsEnabled);
        Assert.Null(value.DefaultStringValue);
        Assert.Null(value.DefaultNumberValue);
        Assert.Null(value.DefaultJsonValue);
    }

    [Fact]
    public void ForString_ForNumber_ForJson_store_default_in_matching_column()
    {
        Assert.Equal("abc", FeatureFlagValue.ForString(FlagId, ScopeId, "abc").DefaultStringValue);
        Assert.Equal(4.5, FeatureFlagValue.ForNumber(FlagId, ScopeId, 4.5).DefaultNumberValue);
        Assert.Equal("{\"a\":1}", FeatureFlagValue.ForJson(FlagId, ScopeId, "{\"a\":1}").DefaultJsonValue);
    }

    [Fact]
    public void Setters_clear_other_typed_columns()
    {
        var value = FeatureFlagValue.ForString(FlagId, ScopeId, "old");

        value.SetNumberDefault(3);
        Assert.Null(value.DefaultStringValue);
        Assert.Equal(3, value.DefaultNumberValue);

        value.SetJsonDefault("[1]");
        Assert.Null(value.DefaultNumberValue);
        Assert.Equal("[1]", value.DefaultJsonValue);

        value.SetStringDefault("new");
        Assert.Null(value.DefaultJsonValue);
        Assert.Equal("new", value.DefaultStringValue);

        value.SetBooleanDefault(true);
        Assert.Null(value.DefaultStringValue);
        Assert.True(value.IsEnabled);
    }

    [Fact]
    public void Setters_bump_UpdatedAt()
    {
        var value = FeatureFlagValue.ForBoolean(FlagId, ScopeId, false);
        value.UpdatedAt = DateTime.MinValue;

        value.SetBooleanDefault(true);

        Assert.True(value.UpdatedAt > DateTime.UtcNow.AddMinutes(-1));
    }

    // Primitive types must stay CLR primitives (not JsonValue) so callers can pattern-match on them.
    [Fact]
    public void ResolveValue_returns_clr_primitives()
    {
        Assert.IsType<bool>(WithFlag(FeatureFlagValue.ForBoolean(FlagId, ScopeId, true), FeatureFlagType.Boolean).ResolveValue());
        Assert.IsType<string>(WithFlag(FeatureFlagValue.ForString(FlagId, ScopeId, "s"), FeatureFlagType.String).ResolveValue());
        Assert.IsType<double>(WithFlag(FeatureFlagValue.ForNumber(FlagId, ScopeId, 2), FeatureFlagType.Number).ResolveValue());
    }

    [Fact]
    public void ResolveValue_serializes_to_value_for_each_type()
    {
        Assert.Equal("true", Serialize(FeatureFlagValue.ForBoolean(FlagId, ScopeId, true), FeatureFlagType.Boolean));
        Assert.Equal("\"s\"", Serialize(FeatureFlagValue.ForString(FlagId, ScopeId, "s"), FeatureFlagType.String));
        Assert.Equal("2", Serialize(FeatureFlagValue.ForNumber(FlagId, ScopeId, 2), FeatureFlagType.Number));
        Assert.Equal("{\"a\":1}", Serialize(FeatureFlagValue.ForJson(FlagId, ScopeId, "{\"a\":1}"), FeatureFlagType.Json));
    }

    private static string Serialize(FeatureFlagValue value, FeatureFlagType type) =>
        JsonSerializer.Serialize(WithFlag(value, type).ResolveValue());

    [Fact]
    public void ResolveValue_returns_null_for_missing_json()
    {
        var value = WithFlag(FeatureFlagValue.ForJson(FlagId, ScopeId, null), FeatureFlagType.Json);

        Assert.Null(value.ResolveValue());
    }

    [Fact]
    public void ResolveValue_throws_for_unknown_type()
    {
        var value = WithFlag(FeatureFlagValue.ForBoolean(FlagId, ScopeId, true), (FeatureFlagType)99);

        Assert.Throws<ArgumentOutOfRangeException>(() => value.ResolveValue());
    }
}
