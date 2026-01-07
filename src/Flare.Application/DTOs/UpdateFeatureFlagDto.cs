using System.ComponentModel.DataAnnotations;

namespace Flare.Application.DTOs;

public class UpdateFeatureFlagDto
{
    [Required]
    [MinLength(2, ErrorMessage = "Name must be at least 2 characters long")]
    [MaxLength(255, ErrorMessage = "Name cannot exceed 255 characters")]
    public required string Name { get; init; }
    [Required]
    [MinLength(2, ErrorMessage = "Name must be at least 2 characters long")]
    [MaxLength(255, ErrorMessage = "Name cannot exceed 255 characters")]
    public required string Key { get; init; }

    [MaxLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
    public string? Description { get; init; }
}
