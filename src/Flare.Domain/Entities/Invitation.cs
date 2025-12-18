using Domian.Enums;

namespace Domian.Entities;

public class Invitation
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public Guid InvitedBy { get; set; }
    public User Inviter { get; set; } = null!;
    public string Token { get; set; } = string.Empty;
    public InvitationStatus Status { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}