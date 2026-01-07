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
public class ScopesController : ControllerBase
{
    private readonly IScopeService _scopeService;
    private readonly ILogger<ScopesController> _logger;

    public ScopesController(IScopeService scopeService, ILogger<ScopesController> logger)
    {
        _scopeService = scopeService;
        _logger = logger;
    }

    [HttpPost("projects/{projectId}/scopes")]
    [Authorize]
    [ProducesResponseType(typeof(ScopeResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScopeResponseDto>> CreateScope(Guid projectId, [FromBody] CreateScopeDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }
        
        var userId = HttpContext.GetCurrentUserId()!.Value;
        var result = await _scopeService.CreateAsync(projectId, dto, userId);

        return CreatedAtAction(nameof(GetScope), new { scopeId = result.Id }, result);
    }

    [HttpGet("projects/{projectId}/scopes")]
    [Authorize]
    [ProducesResponseType(typeof(List<ScopeResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<ScopeResponseDto>>> GetScopes(Guid projectId)
    {
        var userId = HttpContext.GetCurrentUserId()!.Value;
        var result = await _scopeService.GetByProjectIdAsync(projectId, userId);

        return Ok(result);
    }

    [HttpGet("scopes/{scopeId}")]
    [Authorize]
    [ProducesResponseType(typeof(ScopeResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScopeResponseDto>> GetScope(Guid scopeId)
    {
        var userId = HttpContext.GetCurrentUserId()!.Value;
        var result = await _scopeService.GetByIdAsync(scopeId, userId);

        return Ok(result);
    }

    [HttpPut("scopes/{scopeId}")]
    [Authorize]
    [ProducesResponseType(typeof(ScopeResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScopeResponseDto>> UpdateScope(Guid scopeId, [FromBody] UpdateScopeDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }
        var userId = HttpContext.GetCurrentUserId()!.Value;
        var result = await _scopeService.UpdateAsync(scopeId, dto, userId);

        return Ok(result);
    }

    [HttpDelete("scopes/{scopeId}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteScope(Guid scopeId)
    {
        var userId = HttpContext.GetCurrentUserId()!.Value;
        await _scopeService.DeleteAsync(scopeId, userId);

        _logger.LogInformation("Scope {ScopeId} deleted by user {UserId}", scopeId, userId);

        return NoContent();
    }
}
