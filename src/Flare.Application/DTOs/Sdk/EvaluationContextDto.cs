using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Flare.Application.DTOs.Sdk;

/// <summary>
/// OpenFeature-compatible evaluation context for flag evaluation requests.
/// </summary>
public class EvaluationContextDto
{
    /// <summary>
    /// The scope alias (environment) for flag evaluation (e.g., "dev", "stage", "production").
    /// </summary>
    [Required(ErrorMessage = "Scope is required")]
    public string Scope { get; set; } = string.Empty;

    /// <summary>
    /// Optional targeting key for user-specific flag evaluation.
    /// </summary>
    public string? TargetingKey { get; set; }

    /// <summary>
    /// Additional custom attributes for evaluation context.
    /// </summary>
    public Dictionary<string, string>? Attributes { get; set; }
}
