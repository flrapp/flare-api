namespace Flare.Domain.Entities;

public class TargetingRule
{
    public Guid Id { get; set; }
    public Guid FeatureFlagValueId { get; set; }
    public FeatureFlagValue FeatureFlagValue { get; set; } = null!;
    public int Priority { get; set; }
    public bool ServeValue { get; set; }
    public ICollection<TargetingCondition> Conditions { get; set; } = new List<TargetingCondition>();
}
