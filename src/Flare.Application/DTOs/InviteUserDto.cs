using System.ComponentModel.DataAnnotations;

namespace Flare.Application.DTOs;

public class InviteUserDto
{
    [Required]
    public Guid UserId { get; set; }
}
