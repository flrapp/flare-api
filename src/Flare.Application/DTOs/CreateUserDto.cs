using System.ComponentModel.DataAnnotations;

namespace Flare.Application.DTOs;

public class CreateUserDto
{
    [Required]
    [MinLength(3, ErrorMessage = "Username must be at least 3 characters long")]
    [MaxLength(50, ErrorMessage = "Username cannot exceed 50 characters")]
    [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Username can only contain letters, numbers, and underscores")]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MinLength(2, ErrorMessage = "Full name must be at least 2 characters long")]
    [MaxLength(255, ErrorMessage = "Full name cannot exceed 255 characters")]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [MinLength(8, ErrorMessage = "Temporary password must be at least 8 characters long")]
    public string TemporaryPassword { get; set; } = string.Empty;
}
