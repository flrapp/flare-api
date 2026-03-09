using System.ComponentModel.DataAnnotations;

namespace Flare.Application.DTOs;

public class ReorderTargetingRulesDto
{
    [Required]
    [MinLength(1)]
    public required List<Guid> RuleIds { get; init; }
}
