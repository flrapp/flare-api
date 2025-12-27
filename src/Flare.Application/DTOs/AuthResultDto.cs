using Flare.Domain.Enums;

namespace Flare.Application.DTOs;

public class AuthResultDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public GlobalRole GlobalRole { get; set; }
}
