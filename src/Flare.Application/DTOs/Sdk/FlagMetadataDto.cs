namespace Flare.Application.DTOs.Sdk;

/// <summary>
/// Metadata about the evaluated feature flag.
/// </summary>
public class FlagMetadataDto
{
    /// <summary>
    /// The scope alias where the flag was evaluated.
    /// </summary>
    public string? ScopeAlias { get; set; }

    /// <summary>
    /// The unique identifier of the scope.
    /// </summary>
    public Guid? ScopeId { get; set; }

    /// <summary>
    /// The timestamp when the flag value was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}
