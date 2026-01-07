using System.ComponentModel.DataAnnotations;

namespace Flare.Application.DTOs;

public class CreateFeatureFlagDto
{
    [Required]
    [MinLength(2, ErrorMessage = "Name must be at least 2 characters long")]
    [MaxLength(255, ErrorMessage = "Name cannot exceed 255 characters")]
    public required string Name { get; init; }
    [MinLength(2, ErrorMessage = "Name must be at least 2 characters long")]
    [MaxLength(255, ErrorMessage = "Name cannot exceed 255 characters")]
    public string? Description { get; init; }
    [MaxLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
    public required string Key { get; init; } 
}
