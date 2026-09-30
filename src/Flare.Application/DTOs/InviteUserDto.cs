using Flare.Application.Validation;
using Flare.Domain.Enums;

namespace Flare.Application.DTOs;

public class InviteUserDto
{
    [NotEmptyGuid]
    public Guid UserId { get; set; }

    public List<ProjectPermission> ProjectPermissions { get; set; } = new();
    public Dictionary<Guid, List<ScopePermission>> ScopePermissions { get; set; } = new();
}
