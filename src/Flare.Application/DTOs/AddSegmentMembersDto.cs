using System.ComponentModel.DataAnnotations;

namespace Flare.Application.DTOs;

public class AddSegmentMembersDto
{
    [Required]
    [MinLength(1, ErrorMessage = "At least one targeting key is required.")]
    public required List<string> TargetingKeys { get; init; }
}
