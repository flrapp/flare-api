using Flare.Domain.Enums;

namespace Flare.Application.DTOs;

public class UpdateUserPermissionsDto
{
    public List<ProjectPermission> ProjectPermissions { get; set; } = new();
    public Dictionary<Guid, List<ScopePermission>> ScopePermissions { get; set; } = new();
}
