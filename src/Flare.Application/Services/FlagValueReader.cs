using System.Text.Json.Nodes;
using Flare.Domain.Entities;
using Flare.Domain.Enums;

namespace Flare.Application.Services;

/// Single dispatch point for reading typed sparse columns from persistence
/// into serializable CLR objects. All code outside the domain layer routes
/// through here instead of switching on FeatureFlagType itself.
internal static class FlagValueReader
{
    public static object? ReadServe(TargetingRule rule, FeatureFlagType type) => type switch
    {
        FeatureFlagType.Boolean => rule.ServeBooleanValue,
        FeatureFlagType.String => rule.ServeStringValue,
        FeatureFlagType.Number => rule.ServeNumberValue,
        FeatureFlagType.Json => string.IsNullOrWhiteSpace(rule.ServeJsonValue) ? null : JsonNode.Parse(rule.ServeJsonValue),
        _ => null
    };

    public static object? ReadDefault(FeatureFlagValue value, FeatureFlagType _) => value.ResolveValue();

    public static string? Variant(object? value) => value switch
    {
        null => null,
        bool b => b ? "enabled" : "disabled",
        _ => null
    };
}
