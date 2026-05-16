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
    public async Task<IActionResult> CreateFeatureFlag(Guid projectId, [FromBody] CreateFeatureFlagDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = HttpContext.GetCurrentUserId()!.Value;
        var username = HttpContext.GetCurrentUsername() ?? "unknown";
        await _featureFlagService.CreateAsync(projectId, dto, userId, username);

        return Created();
    }

    [HttpGet("projects/{projectId}/feature-flags")]
    [Authorize]
    [ProducesResponseType(typeof(PagedResult<FeatureFlagResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<FeatureFlagResponseDto>>> GetFeatureFlags(
        Guid projectId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 25) pageSize = 25;

        var userId = HttpContext.GetCurrentUserId()!.Value;
        var result = await _featureFlagService.GetPagedByProjectIdAsync(projectId, userId, page, pageSize, search);

        return Ok(result);
    }

    [HttpGet("feature-flags/{featureFlagId}")]
    [Authorize]
    [ProducesResponseType(typeof(FeatureFlagResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FeatureFlagResponseDto>> GetFeatureFlag(Guid featureFlagId)
    {
        var userId = HttpContext.GetCurrentUserId()!.Value;
        var result = await _featureFlagService.GetByIdAsync(featureFlagId, userId);
        return Ok(result);
    }

    [HttpPut("feature-flags/{featureFlagId}")]
    [Authorize]
    [ProducesResponseType(typeof(FeatureFlagResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateFeatureFlag(Guid featureFlagId, [FromBody] UpdateFeatureFlagDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = HttpContext.GetCurrentUserId()!.Value;
        var username = HttpContext.GetCurrentUsername() ?? "unknown";
        await _featureFlagService.UpdateAsync(featureFlagId, dto, userId, username);

        return Ok();
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
        var username = HttpContext.GetCurrentUsername() ?? "unknown";
        await _featureFlagService.DeleteAsync(featureFlagId, userId, username);

        return NoContent();
    }

    [HttpPut("feature-flags/{featureFlagId}/values")]
    [Authorize]
    [ProducesResponseType(typeof(FeatureFlagValueDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateFeatureFlagValue(Guid featureFlagId, [FromBody] UpdateFeatureFlagValueDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = HttpContext.GetCurrentUserId()!.Value;
        var username = HttpContext.GetCurrentUsername() ?? "unknown";
        await _featureFlagService.UpdateValueAsync(featureFlagId, dto, userId, username);

        return Ok();
    }
}
