using Flare.Domain.Enums;

namespace Flare.Application.DTOs;

public class TargetingConditionDto
{
    public Guid Id { get; set; }
    public string AttributeKey { get; set; } = string.Empty;
    public ComparisonOperator Operator { get; set; }
    public string Value { get; set; } = string.Empty;
}
