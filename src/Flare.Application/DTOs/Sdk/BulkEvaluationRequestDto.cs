using System.ComponentModel.DataAnnotations;

namespace Flare.Application.DTOs.Sdk;

/// <summary>
/// OpenFeature-compatible request for evaluating all feature flags in a scope.
/// </summary>
public class BulkEvaluationRequestDto
{
    /// <summary>
    /// The evaluation context containing scope and optional targeting information.
    /// </summary>
    [Required(ErrorMessage = "Context is required")]
    public EvaluationContextDto Context { get; set; } = null!;
}
