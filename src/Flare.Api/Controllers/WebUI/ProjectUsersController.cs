using Asp.Versioning;
using Flare.Application.DTOs;
using Flare.Application.Extensions;
using Flare.Application.Interfaces;
using Flare.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Flare.Api.Controllers.WebUI;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/projects/{projectId}/users")]
public class ProjectUsersController : ControllerBase
{
    private readonly IProjectUserService _projectUserService;
    private readonly IUserService _userService;

    public ProjectUsersController(
        IProjectUserService projectUserService,
        IUserService userService)
    {
        _projectUserService = projectUserService;
        _userService = userService;
    }

    [HttpGet("available")]
    [Authorize]
    [ProducesResponseType(typeof(List<AvailableUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<AvailableUserDto>>> GetAvailableUsers(Guid projectId, [FromQuery] string? search = null)
    {
        var users = await _userService.GetAvailableUsersForProjectAsync(projectId, search);
        return Ok(users);
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ProjectUserResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectUserResponseDto>> InviteUser(Guid projectId, [FromBody] InviteUserDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }
        
        var userId = HttpContext.GetCurrentUserId()!.Value;
        var username = HttpContext.GetCurrentUsername() ?? "unknown";
        var result = await _projectUserService.InviteUserAsync(projectId, dto, userId, username);

        return CreatedAtAction(nameof(GetProjectUsers), new { projectId }, result);
    }

    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(PagedResult<ProjectUserResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<ProjectUserResponseDto>>> GetProjectUsers(
        Guid projectId,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 100) pageSize = 100;

        var userId = HttpContext.GetCurrentUserId()!.Value;
        var result = await _projectUserService.GetProjectUsersAsync(projectId, userId, search, page, pageSize);

        return Ok(result);
    }

    [HttpDelete("{userId}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveUser(Guid projectId, Guid userId)
    {
        var currentUserId = HttpContext.GetCurrentUserId()!.Value;
        var username = HttpContext.GetCurrentUsername() ?? "unknown";
        await _projectUserService.RemoveUserAsync(projectId, userId, currentUserId, username);

        return NoContent();
    }

    [HttpPut("{userId}/permissions")]
    [Authorize]
    [ProducesResponseType(typeof(ProjectUserResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectUserResponseDto>> UpdateUserPermissions(Guid projectId, Guid userId, [FromBody] UpdateUserPermissionsDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var currentUserId = HttpContext.GetCurrentUserId()!.Value;
        var username = HttpContext.GetCurrentUsername() ?? "unknown";
        var result = await _projectUserService.UpdateUserPermissionsAsync(projectId, userId, dto, currentUserId, username);

        return Ok(result);
    }
}
