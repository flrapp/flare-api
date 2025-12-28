using System.ComponentModel.DataAnnotations;

namespace Flare.Application.DTOs;

public class UpdateProjectDto
{
    [Required]
    [MinLength(3, ErrorMessage = "Name must be at least 3 characters long")]
    [MaxLength(255, ErrorMessage = "Name cannot exceed 255 characters")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
    public string? Description { get; set; }
}
