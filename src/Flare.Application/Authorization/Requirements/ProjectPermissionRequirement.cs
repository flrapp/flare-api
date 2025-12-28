using Flare.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace Flare.Application.Authorization.Requirements;

public class ProjectPermissionRequirement : IAuthorizationRequirement
{
    public ProjectPermission Permission { get; }

    public ProjectPermissionRequirement(ProjectPermission permission)
    {
        Permission = permission;
    }
}
