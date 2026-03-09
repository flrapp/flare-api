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
public class TargetingRulesController : ControllerBase
{
    private readonly ITargetingRuleService _targetingRuleService;

    public TargetingRulesController(ITargetingRuleService targetingRuleService)
    {
        _targetingRuleService = targetingRuleService;
    }

    [HttpGet("feature-flag-values/{flagValueId}/targeting-rules")]
    [Authorize]
    [ProducesResponseType(typeof(List<TargetingRuleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<TargetingRuleDto>>> GetTargetingRules(Guid flagValueId)
    {
        var userId = HttpContext.GetCurrentUserId()!.Value;
        return Ok(await _targetingRuleService.GetRulesAsync(flagValueId, userId));
    }

    [HttpPost("feature-flag-values/{flagValueId}/targeting-rules")]
    [Authorize]
    [ProducesResponseType(typeof(TargetingRuleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TargetingRuleDto>> CreateTargetingRule(Guid flagValueId, [FromBody] CreateTargetingRuleDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = HttpContext.GetCurrentUserId()!.Value;
        var username = HttpContext.GetCurrentUsername() ?? "unknown";
        var result = await _targetingRuleService.CreateRuleAsync(flagValueId, dto, userId, username);

        return CreatedAtAction(nameof(GetTargetingRules), new { flagValueId }, result);
    }

    [HttpPut("targeting-rules/{ruleId}")]
    [Authorize]
    [ProducesResponseType(typeof(TargetingRuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TargetingRuleDto>> UpdateTargetingRule(Guid ruleId, [FromBody] UpdateTargetingRuleDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = HttpContext.GetCurrentUserId()!.Value;
        var username = HttpContext.GetCurrentUsername() ?? "unknown";
        return Ok(await _targetingRuleService.UpdateRuleAsync(ruleId, dto, userId, username));
    }

    [HttpDelete("targeting-rules/{ruleId}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTargetingRule(Guid ruleId)
    {
        var userId = HttpContext.GetCurrentUserId()!.Value;
        var username = HttpContext.GetCurrentUsername() ?? "unknown";
        await _targetingRuleService.DeleteRuleAsync(ruleId, userId, username);
        return NoContent();
    }

    [HttpPut("feature-flag-values/{flagValueId}/targeting-rules/reorder")]
    [Authorize]
    [ProducesResponseType(typeof(List<TargetingRuleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<TargetingRuleDto>>> ReorderTargetingRules(Guid flagValueId, [FromBody] ReorderTargetingRulesDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = HttpContext.GetCurrentUserId()!.Value;
        var username = HttpContext.GetCurrentUsername() ?? "unknown";
        return Ok(await _targetingRuleService.ReorderRulesAsync(flagValueId, dto, userId, username));
    }

    [HttpPost("targeting-rules/{ruleId}/conditions")]
    [Authorize]
    [ProducesResponseType(typeof(TargetingRuleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TargetingRuleDto>> AddCondition(Guid ruleId, [FromBody] CreateTargetingConditionDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = HttpContext.GetCurrentUserId()!.Value;
        var username = HttpContext.GetCurrentUsername() ?? "unknown";
        var result = await _targetingRuleService.AddConditionAsync(ruleId, dto, userId, username);

        return CreatedAtAction(nameof(GetTargetingRules), new { flagValueId = result.FeatureFlagValueId }, result);
    }
}
