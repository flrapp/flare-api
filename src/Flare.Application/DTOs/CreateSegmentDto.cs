using System.ComponentModel.DataAnnotations;

namespace Flare.Application.DTOs;

public class CreateSegmentDto
{
    [Required]
    [MaxLength(255)]
    public required string Name { get; init; }

    [MaxLength(1000)]
    public string? Description { get; init; }
}
