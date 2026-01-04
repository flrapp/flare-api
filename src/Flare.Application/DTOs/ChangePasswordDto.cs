using System.ComponentModel.DataAnnotations;

namespace Flare.Application.DTOs;

public class ChangePasswordDto
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [MinLength(8, ErrorMessage = "New password must be at least 8 characters long")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$", ErrorMessage = "New password must contain at least one uppercase letter, one lowercase letter, and one number")]
    public string NewPassword { get; set; } = string.Empty;
}
