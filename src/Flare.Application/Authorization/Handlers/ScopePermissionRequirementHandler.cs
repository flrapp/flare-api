using System.Security.Claims;
using Flare.Application.Authorization.Requirements;
using Flare.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Flare.Application.Authorization.Handlers;

public class ScopePermissionRequirementHandler : AuthorizationHandler<ScopePermissionRequirement>
{
    private readonly IPermissionService _permissionService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ScopePermissionRequirementHandler(
        IPermissionService permissionService,
        IHttpContextAccessor httpContextAccessor)
    {
        _permissionService = permissionService;
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ScopePermissionRequirement requirement)
    {
        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return;
        }

        var scopeId = GetScopeIdFromRoute();
        if (scopeId == null)
        {
            return;
        }

        // Check if user has the required scope permission (with admin bypass)
        var hasPermission = await _permissionService.HasScopePermissionAsync(
            userId,
            scopeId.Value,
            requirement.Permission);

        if (hasPermission)
        {
            context.Succeed(requirement);
        }
    }

    private Guid? GetScopeIdFromRoute()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return null;
        }

        var routeData = httpContext.GetRouteData();

        // Try to get scopeId from route values
        if (routeData?.Values.TryGetValue("scopeId", out var scopeIdValue) == true)
        {
            if (Guid.TryParse(scopeIdValue?.ToString(), out var scopeId))
            {
                return scopeId;
            }
        }

        // Try to get scopeId from query string
        if (httpContext.Request.Query.TryGetValue("scopeId", out var queryScopeId))
        {
            if (Guid.TryParse(queryScopeId.FirstOrDefault(), out var scopeId))
            {
                return scopeId;
            }
        }

        // Also try to get from request body (for UpdateFeatureFlagValue endpoint)
        // Note: This is a simplified approach; in production you might want to use a custom attribute
        // or middleware to extract scopeId from request body if needed

        return null;
    }
}
