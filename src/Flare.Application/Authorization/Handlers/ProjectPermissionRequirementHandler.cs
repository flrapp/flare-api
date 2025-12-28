using System.Security.Claims;
using Flare.Application.Authorization.Requirements;
using Flare.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Flare.Application.Authorization.Handlers;

public class ProjectPermissionRequirementHandler : AuthorizationHandler<ProjectPermissionRequirement>
{
    private readonly IPermissionService _permissionService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ProjectPermissionRequirementHandler(
        IPermissionService permissionService,
        IHttpContextAccessor httpContextAccessor)
    {
        _permissionService = permissionService;
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ProjectPermissionRequirement requirement)
    {
        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return;
        }

        var projectId = GetProjectIdFromRoute();
        if (projectId == null)
        {
            return;
        }

        // Check if user has the required permission (with admin bypass)
        var hasPermission = await _permissionService.HasProjectPermissionAsync(
            userId,
            projectId.Value,
            requirement.Permission);

        if (hasPermission)
        {
            context.Succeed(requirement);
        }
    }

    private Guid? GetProjectIdFromRoute()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return null;
        }

        var routeData = httpContext.GetRouteData();

        // Try to get projectId from route values
        if (routeData?.Values.TryGetValue("projectId", out var projectIdValue) == true)
        {
            if (Guid.TryParse(projectIdValue?.ToString(), out var projectId))
            {
                return projectId;
            }
        }

        // Try to get projectId from query string
        if (httpContext.Request.Query.TryGetValue("projectId", out var queryProjectId))
        {
            if (Guid.TryParse(queryProjectId.FirstOrDefault(), out var projectId))
            {
                return projectId;
            }
        }

        return null;
    }
}
