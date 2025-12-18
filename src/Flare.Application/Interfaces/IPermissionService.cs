using Domian.Enums;

namespace Flare.Application.Interfaces;

public interface IPermissionService
{
    Task<ProjectRole?> GetUserProjectRoleAsync(Guid userId, Guid projectId);
    Task<bool> HasProjectAccessAsync(Guid userId, Guid projectId, ProjectRole minimumRole);
    Task<bool> IsProjectOwnerAsync(Guid userId, Guid projectId);
    Task<bool> IsProjectMemberAsync(Guid userId, Guid projectId);
    Task<bool> CanManageProjectAsync(Guid userId, Guid projectId);
}
