namespace Flare.Application.DTOs;

public class AvailableUserDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool IsAlreadyMember { get; set; }
}
