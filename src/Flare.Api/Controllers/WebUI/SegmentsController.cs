using Asp.Versioning;
using Flare.Application.DTOs;
using Flare.Application.Extensions;
using Flare.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Flare.Api.Controllers.WebUI;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/projects/{projectId}/segments")]
public class SegmentsController : ControllerBase
{
    private readonly ISegmentService _segmentService;

    public SegmentsController(ISegmentService segmentService)
    {
        _segmentService = segmentService;
    }

    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(List<SegmentResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<SegmentResponseDto>>> GetSegments(Guid projectId)
    {
        var userId = HttpContext.GetCurrentUserId()!.Value;
        return Ok(await _segmentService.GetByProjectIdAsync(projectId, userId));
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(SegmentResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SegmentResponseDto>> CreateSegment(Guid projectId, [FromBody] CreateSegmentDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = HttpContext.GetCurrentUserId()!.Value;
        var username = HttpContext.GetCurrentUsername() ?? "unknown";
        var result = await _segmentService.CreateAsync(projectId, dto, userId, username);

        return CreatedAtAction(nameof(GetSegments), new { projectId }, result);
    }

    [HttpPut("{segmentId}")]
    [Authorize]
    [ProducesResponseType(typeof(SegmentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SegmentResponseDto>> UpdateSegment(Guid projectId, Guid segmentId, [FromBody] UpdateSegmentDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = HttpContext.GetCurrentUserId()!.Value;
        var username = HttpContext.GetCurrentUsername() ?? "unknown";
        return Ok(await _segmentService.UpdateAsync(segmentId, dto, userId, username));
    }

    [HttpDelete("{segmentId}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSegment(Guid projectId, Guid segmentId)
    {
        var userId = HttpContext.GetCurrentUserId()!.Value;
        var username = HttpContext.GetCurrentUsername() ?? "unknown";
        await _segmentService.DeleteAsync(segmentId, userId, username);
        return NoContent();
    }
}
