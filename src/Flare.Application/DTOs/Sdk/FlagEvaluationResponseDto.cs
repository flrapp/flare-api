using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Flare.Domain.Enums;

namespace Flare.Application.DTOs.Sdk;

/// <summary>
/// OpenFeature-compatible response for a feature flag evaluation.
/// </summary>
public record FlagEvaluationResponseDto
{
    /// <summary>
    /// The key of the evaluated feature flag.
    /// </summary>
    [Required]
    public string FlagKey { get; set; } = string.Empty;

    /// <summary>
    /// The resolved value of the feature flag. Shape depends on the flag type:
    /// boolean, string, number, or a JSON object/array.
    /// </summary>
    public object? Value { get; init; }

    /// <summary>
    /// The variant name (type-dependent).
    /// </summary>
    public string? Variant { get; init; }

    /// <summary>
    /// The reason for the evaluation result.
    /// Common values: "STATIC", "TARGETING_MATCH", "DEFAULT", "ERROR".
    /// </summary>
    public string Reason { get; init; } = "STATIC";
    [JsonConverter(typeof(JsonStringEnumConverter<FeatureFlagType>))]
    public FeatureFlagType Type { get; init; }
}
