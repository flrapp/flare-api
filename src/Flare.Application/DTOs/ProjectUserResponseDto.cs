using Flare.Domain.Enums;

namespace Flare.Application.DTOs;

public class ProjectUserResponseDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
    public List<ProjectPermission> ProjectPermissions { get; set; } = new();
    public Dictionary<Guid, List<ScopePermission>> ScopePermissions { get; set; } = new();
}
