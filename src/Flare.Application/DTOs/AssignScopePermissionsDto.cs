using System.ComponentModel.DataAnnotations;
using Flare.Application.Validation;
using Flare.Domain.Enums;

namespace Flare.Application.DTOs;

public class AssignScopePermissionsDto
{
    [NotEmptyGuid]
    public Guid ScopeId { get; set; }

    [Required]
    public List<ScopePermission> Permissions { get; set; } = new();
}
