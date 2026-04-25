using System.ComponentModel.DataAnnotations;

namespace Flare.Application.DTOs;

public class UpdateTargetingRuleDto
{
    [Required]
    public required TypedValueDto ServeValue { get; init; }

    [Required]
    public int Priority { get; init; }
}
