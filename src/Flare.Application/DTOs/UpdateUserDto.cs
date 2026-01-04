using System.ComponentModel.DataAnnotations;
using Flare.Domain.Enums;

namespace Flare.Application.DTOs;

public class UpdateUserDto
{
    [Required]
    [MinLength(2, ErrorMessage = "Full name must be at least 2 characters long")]
    [MaxLength(255, ErrorMessage = "Full name cannot exceed 255 characters")]
    public string FullName { get; set; } = string.Empty;

    public GlobalRole GlobalRole { get; set; }
}
