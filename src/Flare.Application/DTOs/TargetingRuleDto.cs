namespace Flare.Application.DTOs;

public class TargetingRuleDto
{
    public Guid Id { get; set; }
    public Guid FeatureFlagValueId { get; set; }
    public int Priority { get; set; }
    public bool ServeValue { get; set; }
    public List<TargetingConditionDto> Conditions { get; set; } = new();
}
