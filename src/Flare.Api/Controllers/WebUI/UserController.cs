using Asp.Versioning;
using Flare.Application.Authorization;
using Flare.Application.DTOs;
using Flare.Application.Extensions;
using Flare.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Flare.Api.Controllers.WebUI;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/users")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;

    public UserController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponseDto>> CreateUser([FromBody] CreateUserDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var currentUserId = HttpContext.GetCurrentUserId()!.Value;
        var username = HttpContext.GetCurrentUsername() ?? "unknown";
        var user = await _userService.CreateUserAsync(dto, currentUserId, username);

        return CreatedAtAction(nameof(GetAllUsers), null, user);
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<UserResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<UserResponseDto>>> GetAllUsers([FromQuery] bool? isActive = null)
    {
        var users = await _userService.GetAllUsersAsync(isActive);
        return Ok(users);
    }

    [HttpPut("{userId:guid}")]
    [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponseDto>> UpdateUser(Guid userId, [FromBody] UpdateUserDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var username = HttpContext.GetCurrentUsername() ?? "unknown";
        var user = await _userService.UpdateUserAsync(userId, dto, username);

        return Ok(user);
    }

    [HttpDelete("{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUser(Guid userId)
    {
        var currentUserId = HttpContext.GetCurrentUserId()!.Value;
        var username = HttpContext.GetCurrentUsername() ?? "unknown";
        await _userService.HardDeleteUserAsync(userId, currentUserId, username);
        return NoContent();
    }

    [HttpPost("{userId:guid}/activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivateUser(Guid userId)
    {
        var username = HttpContext.GetCurrentUsername() ?? "unknown";
        await _userService.ActivateUserAsync(userId, username);
        return NoContent();
    }

    [HttpPost("{userId:guid}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateUser(Guid userId)
    {
        var currentUserId = HttpContext.GetCurrentUserId()!.Value;
        var username = HttpContext.GetCurrentUsername() ?? "unknown";
        await _userService.DeactivateUserAsync(userId, currentUserId, username);
        return NoContent();
    }

    [HttpPost("{userId:guid}/unlock")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnlockUser(Guid userId)
    {
        var username = HttpContext.GetCurrentUsername() ?? "unknown";
        await _userService.UnlockUserAsync(userId, username);
        return NoContent();
    }

    [HttpPost("{userId:guid}/reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetUserPassword(Guid userId, [FromBody] ResetUserPasswordDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var username = HttpContext.GetCurrentUsername() ?? "unknown";
        await _userService.ResetUserPasswordAsync(userId, dto, username);
        return NoContent();
    }
}
