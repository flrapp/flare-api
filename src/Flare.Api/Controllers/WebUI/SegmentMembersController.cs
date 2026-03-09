using Asp.Versioning;
using Flare.Application.DTOs;
using Flare.Application.Extensions;
using Flare.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Flare.Api.Controllers.WebUI;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/segments/{segmentId}/members")]
public class SegmentMembersController : ControllerBase
{
    private readonly ISegmentService _segmentService;

    public SegmentMembersController(ISegmentService segmentService)
    {
        _segmentService = segmentService;
    }

    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(List<SegmentMemberResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<SegmentMemberResponseDto>>> GetMembers(Guid segmentId)
    {
        var userId = HttpContext.GetCurrentUserId()!.Value;
        return Ok(await _segmentService.GetMembersAsync(segmentId, userId));
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(List<SegmentMemberResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<SegmentMemberResponseDto>>> AddMembers(Guid segmentId, [FromBody] AddSegmentMembersDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = HttpContext.GetCurrentUserId()!.Value;
        var username = HttpContext.GetCurrentUsername() ?? "unknown";
        return Ok(await _segmentService.AddMembersAsync(segmentId, dto, userId, username));
    }

    [HttpDelete("{memberKey}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteMember(Guid segmentId, string memberKey)
    {
        var userId = HttpContext.GetCurrentUserId()!.Value;
        var username = HttpContext.GetCurrentUsername() ?? "unknown";
        await _segmentService.DeleteMemberAsync(segmentId, memberKey, userId, username);
        return NoContent();
    }
}
