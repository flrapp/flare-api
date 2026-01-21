namespace Flare.Application.DTOs.Sdk;

/// <summary>
/// OpenFeature-compatible response for bulk feature flag evaluation.
/// </summary>
public class BulkEvaluationResponseDto
{
    /// <summary>
    /// List of all evaluated feature flags.
    /// </summary>
    public List<FlagEvaluationResponseDto> Flags { get; set; } = new();
}
