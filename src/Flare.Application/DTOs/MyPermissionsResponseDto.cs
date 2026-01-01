using Flare.Domain.Enums;

namespace Flare.Application.DTOs;

public class MyPermissionsResponseDto
{
    public Guid UserId { get; set; }
    public Guid ProjectId { get; set; }
    public List<ProjectPermission> ProjectPermissions { get; set; } = new();
    public Dictionary<Guid, List<ScopePermission>> ScopePermissions { get; set; } = new();
}
