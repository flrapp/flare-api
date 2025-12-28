using Flare.Application.Interfaces;
using Flare.Domain.Enums;
using Flare.Infrastructure.Data.Repositories.Interfaces;

namespace Flare.Application.Services;

public class PermissionService : IPermissionService
{
    private readonly IProjectUserRepository _projectUserRepository;
    private readonly IUserRepository _userRepository;
    private readonly IScopeRepository _scopeRepository;

    public PermissionService(
        IProjectUserRepository projectUserRepository,
        IUserRepository userRepository,
        IScopeRepository scopeRepository)
    {
        _projectUserRepository = projectUserRepository;
        _userRepository = userRepository;
        _scopeRepository = scopeRepository;
    }
    
    public async Task<bool> IsProjectMemberAsync(Guid userId, Guid projectId)
    {
        if (await IsAdminAsync(userId))
        {
            return true;
        }

        return await _projectUserRepository.ExistsAsync(userId, projectId);
    }

    #region New granular permission methods

    public async Task<bool> IsAdminAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        return user?.GlobalRole == GlobalRole.Admin;
    }

    public async Task<bool> HasProjectPermissionAsync(Guid userId, Guid projectId, ProjectPermission permission)
    {
        // Admin bypass: Admins have all project permissions
        if (await IsAdminAsync(userId))
        {
            return true;
        }

        return await _projectUserRepository.HasProjectPermissionAsync(userId, projectId, permission);
    }

    public async Task<List<ProjectPermission>> GetUserProjectPermissionsAsync(Guid userId, Guid projectId)
    {
        // Admin bypass: Admins have all permissions
        if (await IsAdminAsync(userId))
        {
            return Enum.GetValues<ProjectPermission>().ToList();
        }

        return await _projectUserRepository.GetUserProjectPermissionsAsync(userId, projectId);
    }

    public async Task<bool> HasScopePermissionAsync(Guid userId, Guid scopeId, ScopePermission permission)
    {
        // Admin bypass: Admins have all scope permissions
        if (await IsAdminAsync(userId))
        {
            return true;
        }

        return await _projectUserRepository.HasScopePermissionAsync(userId, scopeId, permission);
    }

    public async Task<Dictionary<Guid, List<ScopePermission>>> GetUserScopePermissionsAsync(Guid userId, Guid projectId)
    {
        // Admin bypass: Admins have all permissions for all scopes
        if (await IsAdminAsync(userId))
        {
            var scopes = await _scopeRepository.GetByProjectIdAsync(projectId);
            var allScopePermissions = Enum.GetValues<ScopePermission>().ToList();

            return scopes.ToDictionary(
                scope => scope.Id,
                scope => allScopePermissions
            );
        }

        return await _projectUserRepository.GetUserScopePermissionsAsync(userId, projectId);
    }

    #endregion
}
