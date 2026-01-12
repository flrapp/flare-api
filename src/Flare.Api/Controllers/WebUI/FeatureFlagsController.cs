using Asp.Versioning;
using Flare.Application.DTOs;
using Flare.Application.Extensions;
using Flare.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Flare.Api.Controllers.WebUI;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
public class FeatureFlagsController : ControllerBase
{
    private readonly IFeatureFlagService _featureFlagService;

    public FeatureFlagsController(IFeatureFlagService featureFlagService)
    {
        _featureFlagService = featureFlagService;
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

        var userId = HttpContext.GetCurrentUserId()!.Value;
        var result = await _featureFlagService.CreateAsync(projectId, dto, userId);

        return CreatedAtAction(nameof(GetFeatureFlags), new { projectId = result.ProjectId }, result);
    }

    [HttpGet("projects/{projectId}/feature-flags")]
    [Authorize]
    [ProducesResponseType(typeof(List<FeatureFlagResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<FeatureFlagResponseDto>>> GetFeatureFlags(Guid projectId)
    {
        var userId = HttpContext.GetCurrentUserId()!.Value;
        var result = await _featureFlagService.GetByProjectIdAsync(projectId, userId);

        return Ok(result);
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

        var userId = HttpContext.GetCurrentUserId()!.Value;
        var result = await _featureFlagService.UpdateAsync(featureFlagId, dto, userId);

        return Ok(result);
    }

    [HttpDelete("feature-flags/{featureFlagId}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteFeatureFlag(Guid featureFlagId)
    {
        var userId = HttpContext.GetCurrentUserId()!.Value;
        await _featureFlagService.DeleteAsync(featureFlagId, userId);

        return NoContent();
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

        var userId = HttpContext.GetCurrentUserId()!.Value;
        var result = await _featureFlagService.UpdateValueAsync(featureFlagId, dto, userId);

        return Ok(result);
    }
}
