using System.ComponentModel.DataAnnotations;

namespace Flare.Application.Validation;

/// [Required] never fails for a non-nullable Guid (a missing value binds to Guid.Empty),
/// so identifiers that must be supplied use this attribute instead.
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class NotEmptyGuidAttribute : ValidationAttribute
{
    public NotEmptyGuidAttribute() : base("The {0} field is required.")
    {
    }

    public override bool IsValid(object? value) => value is Guid guid && guid != Guid.Empty;
}
