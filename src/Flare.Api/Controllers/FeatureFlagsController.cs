using Asp.Versioning;
using Flare.Application.DTOs;
using Flare.Application.Extensions;
using Flare.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Flare.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
public class FeatureFlagsController : ControllerBase
{
    private readonly IFeatureFlagService _featureFlagService;
    private readonly ILogger<FeatureFlagsController> _logger;

    public FeatureFlagsController(IFeatureFlagService featureFlagService, ILogger<FeatureFlagsController> logger)
    {
        _featureFlagService = featureFlagService;
        _logger = logger;
    }

    [HttpPost("projects/{projectId}/feature-flags")]
    [Authorize]
    [ProducesResponseType(typeof(FeatureFlagResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FeatureFlagResponseDto>> CreateFeatureFlag(Guid projectId, [FromBody] CreateFeatureFlagDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var userId = HttpContext.GetCurrentUserId()!.Value;
            var result = await _featureFlagService.CreateAsync(projectId, dto, userId);

            _logger.LogInformation("Feature flag {FeatureFlagId} created in project {ProjectId} by user {UserId}",
                result.Id, projectId, userId);

            return CreatedAtAction(nameof(GetFeatureFlag), new { featureFlagId = result.Id }, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating feature flag in project {ProjectId}", projectId);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("projects/{projectId}/feature-flags")]
    [Authorize]
    [ProducesResponseType(typeof(List<FeatureFlagResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<FeatureFlagResponseDto>>> GetFeatureFlags(Guid projectId)
    {
        try
        {
            var userId = HttpContext.GetCurrentUserId()!.Value;
            var result = await _featureFlagService.GetByProjectIdAsync(projectId, userId);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving feature flags for project {ProjectId}", projectId);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("feature-flags/{featureFlagId}")]
    [Authorize]
    [ProducesResponseType(typeof(FeatureFlagResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FeatureFlagResponseDto>> GetFeatureFlag(Guid featureFlagId)
    {
        try
        {
            var userId = HttpContext.GetCurrentUserId()!.Value;
            var result = await _featureFlagService.GetByIdAsync(featureFlagId, userId);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving feature flag {FeatureFlagId}", featureFlagId);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("feature-flags/{featureFlagId}")]
    [Authorize]
    [ProducesResponseType(typeof(FeatureFlagResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FeatureFlagResponseDto>> UpdateFeatureFlag(Guid featureFlagId, [FromBody] UpdateFeatureFlagDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var userId = HttpContext.GetCurrentUserId()!.Value;
            var result = await _featureFlagService.UpdateAsync(featureFlagId, dto, userId);

            _logger.LogInformation("Feature flag {FeatureFlagId} updated by user {UserId}", featureFlagId, userId);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating feature flag {FeatureFlagId}", featureFlagId);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("feature-flags/{featureFlagId}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteFeatureFlag(Guid featureFlagId)
    {
        try
        {
            var userId = HttpContext.GetCurrentUserId()!.Value;
            await _featureFlagService.DeleteAsync(featureFlagId, userId);

            _logger.LogInformation("Feature flag {FeatureFlagId} deleted by user {UserId}", featureFlagId, userId);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting feature flag {FeatureFlagId}", featureFlagId);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("feature-flags/{featureFlagId}/values")]
    [Authorize]
    [ProducesResponseType(typeof(FeatureFlagValueDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FeatureFlagValueDto>> UpdateFeatureFlagValue(Guid featureFlagId, [FromBody] UpdateFeatureFlagValueDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var userId = HttpContext.GetCurrentUserId()!.Value;
            var result = await _featureFlagService.UpdateValueAsync(featureFlagId, dto, userId);

            _logger.LogInformation("Feature flag {FeatureFlagId} value updated for scope {ScopeId} by user {UserId}",
                featureFlagId, dto.ScopeId, userId);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating feature flag {FeatureFlagId} value", featureFlagId);
            return BadRequest(new { message = ex.Message });
        }
    }
}
