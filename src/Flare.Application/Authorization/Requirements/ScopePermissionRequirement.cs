using Flare.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace Flare.Application.Authorization.Requirements;

public class ScopePermissionRequirement : IAuthorizationRequirement
{
    public ScopePermission Permission { get; }

    public ScopePermissionRequirement(ScopePermission permission)
    {
        Permission = permission;
    }
}
