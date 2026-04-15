using System.Text.Json.Nodes;
using Flare.Domain.Enums;

namespace Flare.Domain.Entities;

public class FeatureFlagValue
{
    public Guid Id { get; set; }
    public Guid FeatureFlagId { get; set; }
    public FeatureFlag FeatureFlag { get; set; } = null!;
    public Guid ScopeId { get; set; }
    public Scope Scope { get; set; } = null!;
    public bool IsEnabled { get; set; }
    public DateTime UpdatedAt { get; set; }

    public string? DefaultStringValue { get; private set; }
    public double? DefaultNumberValue { get; private set; }
    public string? DefaultJsonValue { get; private set; }

    public ICollection<TargetingRule> TargetingRules { get; set; } = new List<TargetingRule>();

    public static FeatureFlagValue ForBoolean(Guid flagId, Guid scopeId, bool defaultEnabled) =>
        new()
        {
            Id = Guid.NewGuid(),
            FeatureFlagId = flagId,
            ScopeId = scopeId,
            IsEnabled = defaultEnabled,
            UpdatedAt = DateTime.UtcNow
        };

    public static FeatureFlagValue ForString(Guid flagId, Guid scopeId, string? defaultValue) =>
        new()
        {
            Id = Guid.NewGuid(),
            FeatureFlagId = flagId,
            ScopeId = scopeId,
            DefaultStringValue = defaultValue,
            UpdatedAt = DateTime.UtcNow
        };

    public static FeatureFlagValue ForNumber(Guid flagId, Guid scopeId, double? defaultValue) =>
        new()
        {
            Id = Guid.NewGuid(),
            FeatureFlagId = flagId,
            ScopeId = scopeId,
            DefaultNumberValue = defaultValue,
            UpdatedAt = DateTime.UtcNow
        };

    public static FeatureFlagValue ForJson(Guid flagId, Guid scopeId, string? defaultJson) =>
        new()
        {
            Id = Guid.NewGuid(),
            FeatureFlagId = flagId,
            ScopeId = scopeId,
            DefaultJsonValue = defaultJson,
            UpdatedAt = DateTime.UtcNow
        };

    public void SetBooleanDefault(bool enabled)
    {
        ClearDefaults();
        IsEnabled = enabled;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetStringDefault(string? value)
    {
        ClearDefaults();
        DefaultStringValue = value;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetNumberDefault(double? value)
    {
        ClearDefaults();
        DefaultNumberValue = value;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetJsonDefault(string? value)
    {
        ClearDefaults();
        DefaultJsonValue = value;
        UpdatedAt = DateTime.UtcNow;
    }

    private void ClearDefaults()
    {
        DefaultStringValue = null;
        DefaultNumberValue = null;
        DefaultJsonValue = null;
    }

    public object? ResolveValue() => FeatureFlag.Type switch
    {
        FeatureFlagType.Boolean => IsEnabled,
        FeatureFlagType.String => DefaultStringValue,
        FeatureFlagType.Number => DefaultNumberValue,
        FeatureFlagType.Json => DefaultJsonValue is null ? null : JsonNode.Parse(DefaultJsonValue),
        _ => throw new ArgumentOutOfRangeException(nameof(FeatureFlag.Type), FeatureFlag.Type, "Unsupported flag type.")
    };
}
