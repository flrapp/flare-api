using System.ComponentModel.DataAnnotations;

namespace Flare.Application.DTOs;

public class RegisterDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [MinLength(2)]
    [MaxLength(255)]
    public string FullName { get; set; } = string.Empty;
}
