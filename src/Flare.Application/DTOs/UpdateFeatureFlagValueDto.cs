using System.ComponentModel.DataAnnotations;

namespace Flare.Application.DTOs;

public class UpdateFeatureFlagValueDto
{
    [Required]
    public Guid ScopeId { get; set; }

    [Required]
    public bool IsEnabled { get; set; }
}
