using System.Security.Claims;
using Domian.Enums;
using Flare.Application.Authorization.Requirements;
using Microsoft.AspNetCore.Authorization;

namespace Flare.Application.Authorization.Handlers;

public class AdminRequirementHandler : AuthorizationHandler<AdminRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminRequirement requirement)
    {
        var roleClaim = context.User.FindFirst(ClaimTypes.Role);

        if (roleClaim?.Value == GlobalRole.Admin.ToString())
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
