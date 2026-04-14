using Flare.Domain.Enums;

namespace Flare.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public GlobalRole GlobalRole { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool MustChangePassword { get; set; } = false;
    public bool InitialUser { get; set; } = false;

    public int FailedLoginAttempts { get; set; } = 0;
    public DateTime? LastFailedLoginAt { get; set; }
    public DateTime? LockedUntil { get; set; }
    public bool IsBruteForceLocked { get; set; } = false;

    public ICollection<ProjectUser> ProjectMemberships { get; set; } = new List<ProjectUser>();
}