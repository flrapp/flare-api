namespace Flare.Domain.Entities;

public class Scope
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string Alias { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<FeatureFlagValue> FeatureFlagValues { get; set; } = new List<FeatureFlagValue>();
}
