using System.ComponentModel.DataAnnotations;

namespace Flare.Application.DTOs;

public class CreateTargetingRuleDto
{
    [Required]
    public bool ServeValue { get; init; }

    [Required]
    [MinLength(1, ErrorMessage = "At least one condition is required.")]
    public required List<CreateTargetingConditionDto> Conditions { get; init; }
}
