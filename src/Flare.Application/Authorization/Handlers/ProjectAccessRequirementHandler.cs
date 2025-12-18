using System.Security.Claims;
using Flare.Application.Authorization.Requirements;
using Flare.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Flare.Application.Authorization.Handlers;

public class ProjectAccessRequirementHandler : AuthorizationHandler<ProjectAccessRequirement>
{
    private readonly IPermissionService _permissionService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ProjectAccessRequirementHandler(
        IPermissionService permissionService,
        IHttpContextAccessor httpContextAccessor)
    {
        _permissionService = permissionService;
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ProjectAccessRequirement requirement)
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

        var hasAccess = await _permissionService.HasProjectAccessAsync(
            userId,
            projectId.Value,
            requirement.MinimumRole);

        if (hasAccess)
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
