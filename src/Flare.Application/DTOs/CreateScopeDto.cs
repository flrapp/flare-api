using System.ComponentModel.DataAnnotations;

namespace Flare.Application.DTOs;

public class CreateScopeDto
{
    [Required]
    [MinLength(2, ErrorMessage = "Name must be at least 2 characters long")]
    [MaxLength(255, ErrorMessage = "Name cannot exceed 255 characters")]
    public string Name { get; set; } = string.Empty;
    public required string Alias { get; set; }

    [MaxLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
    public string? Description { get; set; }
}
