namespace Flare.Domain.Entities;

public class Project
{
    public Guid Id { get; set; }
    public string Alias { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public Guid CreatedBy { get; set; }
    public User Creator { get; set; } = null!;
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<ProjectUser> Members { get; set; } = new List<ProjectUser>();
    public ICollection<Scope> Scopes { get; set; } = new List<Scope>();
    public ICollection<FeatureFlag> FeatureFlags { get; set; } = new List<FeatureFlag>();
}