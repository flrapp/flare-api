using Flare.Domain.Enums;

namespace Flare.Domain.Entities;

public class ProjectUserScopePermission
{
    public Guid Id { get; set; }
    public Guid ProjectUserId { get; set; }
    public ProjectUser ProjectUser { get; set; } = null!;
    public Guid ScopeId { get; set; }
    public Scope Scope { get; set; } = null!;
    public ScopePermission Permission { get; set; }
}
