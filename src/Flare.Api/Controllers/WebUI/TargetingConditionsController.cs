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
public class TargetingConditionsController : ControllerBase
{
    private readonly ITargetingRuleService _targetingRuleService;

    public TargetingConditionsController(ITargetingRuleService targetingRuleService)
    {
        _targetingRuleService = targetingRuleService;
    }

    [HttpPut("targeting-conditions/{conditionId}")]
    [Authorize]
    [ProducesResponseType(typeof(TargetingRuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCondition(Guid conditionId, [FromBody] UpdateTargetingConditionDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = HttpContext.GetCurrentUserId()!.Value;
        var username = HttpContext.GetCurrentUsername() ?? "unknown";
        await _targetingRuleService.UpdateConditionAsync(conditionId, dto, userId, username);
        return Ok();
    }

    [HttpDelete("targeting-conditions/{conditionId}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCondition(Guid conditionId)
    {
        var userId = HttpContext.GetCurrentUserId()!.Value;
        var username = HttpContext.GetCurrentUsername() ?? "unknown";
        await _targetingRuleService.DeleteConditionAsync(conditionId, userId, username);
        return NoContent();
    }
}
