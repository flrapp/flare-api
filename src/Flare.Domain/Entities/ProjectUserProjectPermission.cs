using Flare.Domain.Enums;

namespace Flare.Domain.Entities;

public class ProjectUserProjectPermission
{
    public Guid Id { get; set; }
    public Guid ProjectUserId { get; set; }
    public ProjectUser ProjectUser { get; set; } = null!;
    public ProjectPermission Permission { get; set; }
}
