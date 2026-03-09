using System.ComponentModel.DataAnnotations;

namespace Flare.Application.DTOs;

public class UpdateTargetingRuleDto
{
    [Required]
    public bool ServeValue { get; init; }

    [Required]
    public int Priority { get; init; }
}
