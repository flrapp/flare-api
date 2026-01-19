using System.ComponentModel.DataAnnotations;

namespace Flare.Application.DTOs.Sdk;

/// <summary>
/// OpenFeature-compatible request for evaluating a feature flag.
/// </summary>
public class FlagEvaluationRequestDto
{
    /// <summary>
    /// The unique key identifying the feature flag.
    /// </summary>
    [Required(ErrorMessage = "FlagKey is required")]
    public string FlagKey { get; set; } = string.Empty;

    /// <summary>
    /// The evaluation context containing scope and optional targeting information.
    /// </summary>
    [Required(ErrorMessage = "Context is required")]
    public EvaluationContextDto Context { get; set; } = null!;
}
