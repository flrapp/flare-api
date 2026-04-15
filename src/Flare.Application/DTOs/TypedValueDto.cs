using System.ComponentModel.DataAnnotations;
using Flare.Domain.Entities;
using Flare.Domain.Enums;
using ValidationException = Flare.Domain.Exceptions.ValidationException;

namespace Flare.Application.DTOs;

/// Wire shape for typed serve value. The flag's own Type (not this one) is authoritative;
/// dispatch happens through FeatureFlag/TargetingRule factories, not on this discriminator.
public class TypedValueDto
{
    [Required]
    public FeatureFlagType Type { get; init; }

    public bool? Bool { get; init; }
    public string? String { get; init; }
    public double? Number { get; init; }
    public string? Json { get; init; }

    public void EnsureMatches(FeatureFlagType parentType)
    {
        if (Type != parentType)
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["serveValue"] = new[] { $"Serve value type '{Type}' does not match flag type '{parentType}'." }
            });
    }

    public TargetingRule BuildRule(int priority, Guid flagValueId, FeatureFlagType parentType, IEnumerable<TargetingCondition> conditions)
    {
        EnsureMatches(parentType);
        return parentType switch
        {
            FeatureFlagType.Boolean => TargetingRule.ForBoolean(flagValueId, priority, RequireBool(), conditions),
            FeatureFlagType.String => TargetingRule.ForString(flagValueId, priority, RequireString(), conditions),
            FeatureFlagType.Number => TargetingRule.ForNumber(flagValueId, priority, RequireNumber(), conditions),
            FeatureFlagType.Json => TargetingRule.ForJson(flagValueId, priority, RequireJson(), conditions),
            _ => throw Missing("type")
        };
    }

    public void ApplyTo(TargetingRule rule, FeatureFlagType parentType)
    {
        EnsureMatches(parentType);
        switch (parentType)
        {
            case FeatureFlagType.Boolean: rule.SetBoolean(RequireBool()); break;
            case FeatureFlagType.String: rule.SetString(RequireString()); break;
            case FeatureFlagType.Number: rule.SetNumber(RequireNumber()); break;
            case FeatureFlagType.Json: rule.SetJson(RequireJson()); break;
        }
    }

    private bool RequireBool() => Bool ?? throw Missing("bool");
    private string RequireString() => String ?? throw Missing("string");
    private double RequireNumber() => Number ?? throw Missing("number");
    private string RequireJson() => Json ?? throw Missing("json");

    private static ValidationException Missing(string field) =>
        new(new Dictionary<string, string[]>
        {
            [$"serveValue.{field}"] = new[] { $"Value for '{field}' is required." }
        });
}
