namespace Flare.Application.DTOs;

public class ScopeResponseDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Alias { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}
