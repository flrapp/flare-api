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
    private readonly ILogger<ProjectUsersController> _logger;

    public ProjectUsersController(
        IProjectUserService projectUserService,
        IUserService userService,
        ILogger<ProjectUsersController> logger)
    {
        _projectUserService = projectUserService;
        _userService = userService;
        _logger = logger;
    }

    [HttpGet("available")]
    [Authorize]
    [ProducesResponseType(typeof(List<AvailableUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<AvailableUserDto>>> GetAvailableUsers(Guid projectId)
    {
        var users = await _userService.GetAvailableUsersForProjectAsync(projectId);
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
        var result = await _projectUserService.InviteUserAsync(projectId, dto, userId);

        _logger.LogInformation("User {InvitedUserId} invited to project {ProjectId} by user {UserId}",
            dto.UserId, projectId, userId);

        return CreatedAtAction(nameof(GetProjectUsers), new { projectId }, result);
    }

    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(List<ProjectUserResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<ProjectUserResponseDto>>> GetProjectUsers(Guid projectId)
    {
        var userId = HttpContext.GetCurrentUserId()!.Value;
        var result = await _projectUserService.GetProjectUsersAsync(projectId, userId);

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
        try
        {
            var currentUserId = HttpContext.GetCurrentUserId()!.Value;
            await _projectUserService.RemoveUserAsync(projectId, userId, currentUserId);

            _logger.LogInformation("User {UserId} removed from project {ProjectId} by user {CurrentUserId}",
                userId, projectId, currentUserId);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing user {UserId} from project {ProjectId}", userId, projectId);
            return BadRequest(new { message = ex.Message });
        }
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

        try
        {
            var currentUserId = HttpContext.GetCurrentUserId()!.Value;
            var result = await _projectUserService.UpdateUserPermissionsAsync(projectId, userId, dto, currentUserId);

            _logger.LogInformation("Permissions updated for user {UserId} in project {ProjectId} by user {CurrentUserId}",
                userId, projectId, currentUserId);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating permissions for user {UserId} in project {ProjectId}", userId, projectId);
            return BadRequest(new { message = ex.Message });
        }
    }
}
