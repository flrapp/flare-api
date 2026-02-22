using System.Security.Claims;
using Flare.Application.DTOs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;

namespace Flare.Application.Extensions;

public static class AuthenticationExtensions
{
    public static async Task SignInUserAsync(this HttpContext httpContext, AuthResultDto authResult)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, authResult.UserId.ToString()),
            new Claim("Username", authResult.Username),
            new Claim(ClaimTypes.Name, authResult.FullName),
            new Claim(ClaimTypes.Role, authResult.GlobalRole.ToString())
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
        };

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);
    }

    public static async Task SignOutUserAsync(this HttpContext httpContext)
    {
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    public static Guid? GetCurrentUserId(this HttpContext httpContext)
    {
        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim != null && Guid.TryParse((string?)userIdClaim.Value, out var userId))
        {
            return userId;
        }
        return null;
    }

    public static string? GetCurrentUsername(this HttpContext httpContext)
        => httpContext.User.FindFirst("Username")?.Value;
}
