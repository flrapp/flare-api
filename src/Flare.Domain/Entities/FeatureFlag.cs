using Flare.Domain.Enums;

namespace Flare.Domain.Entities;

public class FeatureFlag
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public FeatureFlagType Type { get; init; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<FeatureFlagValue> Values { get; set; } = new List<FeatureFlagValue>();

    public FeatureFlagValue CreateValueForScope(Guid scopeId) => Type switch
    {
        FeatureFlagType.Boolean => FeatureFlagValue.ForBoolean(Id, scopeId, false),
        FeatureFlagType.String => FeatureFlagValue.ForString(Id, scopeId, null),
        FeatureFlagType.Number => FeatureFlagValue.ForNumber(Id, scopeId, null),
        FeatureFlagType.Json => FeatureFlagValue.ForJson(Id, scopeId, null),
        _ => throw new ArgumentOutOfRangeException(nameof(Type), Type, "Unsupported flag type.")
    };
}
