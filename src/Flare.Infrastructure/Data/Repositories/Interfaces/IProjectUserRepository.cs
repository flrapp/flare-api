using Flare.Domain.Entities;
using Flare.Domain.Enums;

namespace Flare.Infrastructure.Data.Repositories.Interfaces;

public interface IProjectUserRepository
{
    Task<ProjectUser?> GetByIdAsync(Guid projectUserId);
    Task<ProjectUser?> GetByUserAndProjectAsync(Guid userId, Guid projectId);
    Task<ProjectUser?> GetByUserAndProjectWithPermissionsAsync(Guid userId, Guid projectId);
    Task<List<ProjectUser>> GetByProjectIdAsync(Guid projectId);
    Task<ProjectUser> AddAsync(ProjectUser projectUser);
    Task UpdateAsync(ProjectUser projectUser);
    Task DeleteAsync(Guid projectUserId);
    Task<List<ProjectPermission>> GetUserProjectPermissionsAsync(Guid userId, Guid projectId);
    Task<Dictionary<Guid, List<ScopePermission>>> GetUserScopePermissionsAsync(Guid userId, Guid projectId);
    Task<bool> HasProjectPermissionAsync(Guid userId, Guid projectId, ProjectPermission permission);
    Task<bool> HasScopePermissionAsync(Guid userId, Guid scopeId, ScopePermission permission);
    Task AddProjectPermissionAsync(Guid projectUserId, ProjectPermission permission);
    Task AddProjectPermissionsAsync(Guid projectUserId, IEnumerable<ProjectPermission> permissions);
    Task RemoveProjectPermissionAsync(Guid projectUserId, ProjectPermission permission);
    Task RemoveAllProjectPermissionsAsync(Guid projectUserId);
    Task AddScopePermissionAsync(Guid projectUserId, Guid scopeId, ScopePermission permission);
    Task AddScopePermissionsAsync(Guid projectUserId, IReadOnlyDictionary<Guid, IEnumerable<ScopePermission>> scopePermissions);
    Task RemoveScopePermissionAsync(Guid projectUserId, Guid scopeId, ScopePermission permission);
    Task RemoveAllScopePermissionsAsync(Guid projectUserId);
    Task RemoveAllScopePermissionsForScopeAsync(Guid scopeId);
    Task<bool> ExistsAsync(Guid userId, Guid projectId);
}
