namespace Flare.Application.DTOs;

public class ProjectDetailResponseDto
{
    public Guid Id { get; set; }
    public string Alias { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ApiKey { get; set; }
    public Guid CreatedBy { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int MemberCount { get; set; }
    public int ScopeCount { get; set; }
    public int FeatureFlagCount { get; set; }
}
