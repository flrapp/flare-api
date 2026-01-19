using System.ComponentModel.DataAnnotations;

namespace Flare.Application.DTOs.Sdk;

/// <summary>
/// OpenFeature-compatible response for a feature flag evaluation.
/// </summary>
public class FlagEvaluationResponseDto
{
    /// <summary>
    /// The key of the evaluated feature flag.
    /// </summary>
    [Required]
    public string FlagKey { get; set; } = string.Empty;

    /// <summary>
    /// The resolved boolean value of the feature flag.
    /// </summary>
    [Required]
    public bool Value { get; set; }

    /// <summary>
    /// The variant name ("enabled" or "disabled").
    /// </summary>
    public string? Variant { get; set; }

    /// <summary>
    /// The reason for the evaluation result.
    /// Common values: "STATIC", "TARGETING_MATCH", "DEFAULT", "ERROR".
    /// </summary>
    public string Reason { get; set; } = "STATIC";

    /// <summary>
    /// Additional metadata about the flag evaluation.
    /// </summary>
    public FlagMetadataDto? FlagMetadata { get; set; }
}
