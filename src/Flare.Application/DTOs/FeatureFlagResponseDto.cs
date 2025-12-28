namespace Flare.Application.DTOs;

public class FeatureFlagResponseDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<FeatureFlagValueDto> Values { get; set; } = new();
}
