using System.Security.Claims;
using Flare.Application.Authorization.Requirements;
using Flare.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Flare.Application.Authorization.Handlers;

public class ProjectOwnerRequirementHandler : AuthorizationHandler<ProjectOwnerRequirement>
{
    private readonly IPermissionService _permissionService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ProjectOwnerRequirementHandler(
        IPermissionService permissionService,
        IHttpContextAccessor httpContextAccessor)
    {
        _permissionService = permissionService;
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ProjectOwnerRequirement requirement)
    {
        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse((string?)userIdClaim.Value, out var userId))
        {
            return;
        }

        var projectId = GetProjectIdFromRoute();
        if (projectId == null)
        {
            return;
        }

        var isOwner = await _permissionService.IsProjectOwnerAsync(userId, projectId.Value);

        if (isOwner)
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
        if (routeData?.Values.TryGetValue("projectId", out var projectIdValue) == true)
        {
            if (Guid.TryParse((string?)projectIdValue?.ToString(), out var projectId))
            {
                return projectId;
            }
        }

        if (httpContext.Request.Query.TryGetValue("projectId", out var queryProjectId))
        {
            if (Guid.TryParse(Enumerable.FirstOrDefault<string?>(queryProjectId), out var projectId))
            {
                return projectId;
            }
        }

        return null;
    }
}
