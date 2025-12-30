namespace Flare.Domain.Entities;

public class ProjectUser
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid InvitedBy { get; set; }
    public DateTime JoinedAt { get; set; }

    public ICollection<ProjectUserProjectPermission> ProjectPermissions { get; set; } = new List<ProjectUserProjectPermission>();
    public ICollection<ProjectUserScopePermission> ScopePermissions { get; set; } = new List<ProjectUserScopePermission>();
}
