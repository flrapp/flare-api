using Domian.Enums;
using Microsoft.AspNetCore.Authorization;

namespace Flare.Application.Authorization.Requirements;

public class ProjectAccessRequirement : IAuthorizationRequirement
{
    public ProjectRole MinimumRole { get; }

    public ProjectAccessRequirement(ProjectRole minimumRole)
    {
        MinimumRole = minimumRole;
    }
}
