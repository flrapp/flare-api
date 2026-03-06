using System.ComponentModel.DataAnnotations;

namespace Flare.Application.DTOs;

public class ResetUserPasswordDto
{
    [Required, MinLength(8)]
    public string TemporaryPassword { get; set; } = string.Empty;
}
