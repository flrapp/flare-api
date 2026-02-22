using Asp.Versioning;
using Flare.Api.Attributes;
using Flare.Api.Constants;
using Flare.Application.DTOs.Sdk;
using Flare.Application.Interfaces;
using Flare.Application.Metrics;
using Flare.Domain.Constants;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace Flare.Api.Controllers.Sdk;

/// <summary>
/// OpenFeature-compatible SDK endpoint for feature flag evaluation.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("sdk/v{version:apiVersion}/flags")]
[EnableCors(CorsPolicyConstants.IntegrationCorsPolicy)]
public class FlagsController : ControllerBase
{
    private readonly IFeatureFlagService _featureFlagService;
    private readonly FlareMetrics _metrics;

    public FlagsController(IFeatureFlagService featureFlagService, FlareMetrics metrics)
    {
        _featureFlagService = featureFlagService;
        _metrics = metrics;
    }

    /// <summary>
    /// Evaluates a feature flag using OpenFeature-compatible format.
    /// </summary>
    /// <param name="request">The flag evaluation request containing flag key and evaluation context.</param>
    /// <returns>The flag evaluation response with value, variant, reason, and metadata.</returns>
    [HttpPost("evaluate")]
    [BearerApiKeyAuthorization]
    [ProducesResponseType(typeof(FlagEvaluationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FlagEvaluationResponseDto>> EvaluateFlag(
        [FromBody] FlagEvaluationRequestDto request)
    {
        var projectId = (Guid)HttpContext.Items[HttpContextKeys.ProjectId]!;
        var projectAlias = (string)HttpContext.Items[HttpContextKeys.ProjectAlias]!;

        var result = await _featureFlagService.EvaluateFlagAsync(
            projectId,
            request.FlagKey,
            request.Context);

        _metrics.RecordEvaluation(projectAlias, request.FlagKey, request.Context.Scope);

        return Ok(result);
    }

    /// <summary>
    /// Evaluates all feature flags for a scope using OpenFeature-compatible format.
    /// </summary>
    /// <param name="request">The bulk evaluation request containing evaluation context.</param>
    /// <returns>All flag evaluation responses for the specified scope.</returns>
    [HttpPost("evaluate-all")]
    [BearerApiKeyAuthorization]
    [ProducesResponseType(typeof(BulkEvaluationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BulkEvaluationResponseDto>> EvaluateAllFlags(
        [FromBody] BulkEvaluationRequestDto request)
    {
        var projectId = (Guid)HttpContext.Items[HttpContextKeys.ProjectId]!;
        var projectAlias = (string)HttpContext.Items[HttpContextKeys.ProjectAlias]!;

        var result = await _featureFlagService.EvaluateAllFlagsAsync(projectId, request.Context);

        _metrics.RecordBulkEvaluation(projectAlias, request.Context.Scope);

        return Ok(result);
    }
}
