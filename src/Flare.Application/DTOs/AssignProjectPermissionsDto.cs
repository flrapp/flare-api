using System.ComponentModel.DataAnnotations;
using Flare.Domain.Enums;

namespace Flare.Application.DTOs;

public class AssignProjectPermissionsDto
{
    [Required]
    public List<ProjectPermission> Permissions { get; set; } = new();
}
