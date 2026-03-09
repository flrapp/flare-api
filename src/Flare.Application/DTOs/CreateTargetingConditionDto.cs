using System.ComponentModel.DataAnnotations;
using Flare.Domain.Enums;

namespace Flare.Application.DTOs;

public class CreateTargetingConditionDto
{
    [Required]
    [MaxLength(255)]
    public required string AttributeKey { get; init; }

    [Required]
    public ComparisonOperator Operator { get; init; }

    [Required]
    public required string Value { get; init; }
}
