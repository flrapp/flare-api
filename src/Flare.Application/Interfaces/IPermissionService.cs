using Flare.Domain.Enums;

namespace Flare.Application.Interfaces;

public interface IPermissionService
{
    Task<bool> IsAdminAsync(Guid userId);
    Task<bool> HasProjectPermissionAsync(Guid userId, Guid projectId, ProjectPermission permission);
    Task<List<ProjectPermission>> GetUserProjectPermissionsAsync(Guid userId, Guid projectId);
    Task<bool> HasScopePermissionAsync(Guid userId, Guid scopeId, ScopePermission permission);
    Task<Dictionary<Guid, List<ScopePermission>>> GetUserScopePermissionsAsync(Guid userId, Guid projectId);
    Task<bool> IsProjectMemberAsync(Guid userId, Guid projectId);
}
