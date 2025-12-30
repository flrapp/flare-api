namespace Flare.Domain.Entities;

public class FeatureFlagValue
{
    public Guid Id { get; set; }
    public Guid FeatureFlagId { get; set; }
    public FeatureFlag FeatureFlag { get; set; } = null!;
    public Guid ScopeId { get; set; }
    public Scope Scope { get; set; } = null!;
    public bool IsEnabled { get; set; }
    public DateTime UpdatedAt { get; set; }
}
