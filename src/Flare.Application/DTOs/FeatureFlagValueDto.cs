namespace Flare.Application.DTOs;

public class FeatureFlagValueDto
{
    public Guid Id { get; set; }
    public Guid ScopeId { get; set; }
    public string ScopeName { get; set; } = string.Empty;
    public string ScopeAlias { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTime UpdatedAt { get; set; }
}
