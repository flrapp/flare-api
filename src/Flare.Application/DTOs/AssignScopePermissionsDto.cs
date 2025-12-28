using System.ComponentModel.DataAnnotations;
using Flare.Domain.Enums;

namespace Flare.Application.DTOs;

public class AssignScopePermissionsDto
{
    [Required]
    public Guid ScopeId { get; set; }

    [Required]
    public List<ScopePermission> Permissions { get; set; } = new();
}
