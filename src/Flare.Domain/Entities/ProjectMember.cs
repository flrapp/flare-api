using Flare.Domain.Enums;

namespace Flare.Domain.Entities;

public class ProjectMember
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public ProjectRole ProjectRole { get; set; }
    public Guid InvitedBy { get; set; }
    public DateTime JoinedAt { get; set; }
}