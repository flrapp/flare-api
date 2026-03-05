using Flare.Domain.Enums;

namespace Flare.Domain.Entities;

public class TargetingCondition
{
    public Guid Id { get; set; }
    public Guid TargetingRuleId { get; set; }
    public TargetingRule TargetingRule { get; set; } = null!;
    public string AttributeKey { get; set; } = string.Empty;
    public ComparisonOperator Operator { get; set; }
    public string Value { get; set; } = string.Empty;
}
