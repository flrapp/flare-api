using Asp.Versioning;
using Flare.Application.DTOs;
using Flare.Application.Extensions;
using Flare.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Flare.Api.Controllers.WebUI;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResultDto>> Login([FromBody] LoginDto loginDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var result = await _authService.LoginAsync(loginDto);

            if (result == null)
            {
                _logger.LogWarning("Login failed for username: {Username}", loginDto.Username);
                return Unauthorized(new { message = "Invalid username or password" });
            }

            await HttpContext.SignInUserAsync(result);
            await _authService.UpdateLastLoginAsync(result.UserId);

            _logger.LogInformation("User {Username} logged in successfully", result.Username);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            return BadRequest(new { message = "Login failed. Please try again." });
        }
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout()
    {
        try
        {
            var username = User.FindFirst("Username")?.Value;
            await HttpContext.SignOutUserAsync();

            _logger.LogInformation("User {Username} logged out successfully", username);

            return Ok(new { message = "Logged out successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return BadRequest(new { message = "Logout failed. Please try again." });
        }
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResultDto>> GetCurrentUser()
    {
        try
        {
            var userId = HttpContext.GetCurrentUserId();

            if (userId == null)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var user = await _authService.GetUserByIdAsync(userId.Value);

            if (user == null)
            {
                return Unauthorized(new { message = "User not found" });
            }

            var result = new AuthResultDto
            {
                UserId = user.Id,
                Username = user.Username,
                FullName = user.FullName,
                GlobalRole = user.GlobalRole,
                MustChangePassword = user.MustChangePassword
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current user");
            return BadRequest(new { message = "Failed to retrieve user information" });
        }
    }

    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var userId = HttpContext.GetCurrentUserId();

            if (userId == null)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            await _authService.ChangePasswordAsync(userId.Value, dto);

            _logger.LogInformation("User {UserId} changed password successfully", userId.Value);

            return Ok(new { message = "Password changed successfully" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Password change failed for user: {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password");
            return BadRequest(new { message = "Password change failed. Please try again." });
        }
    }
}
