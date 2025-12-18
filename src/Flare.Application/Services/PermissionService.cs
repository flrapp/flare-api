using Domian.Enums;
using Flare.Application.Interfaces;
using Flare.Infrastructure.Data.Repositories.Interfaces;

namespace Flare.Application.Services;

public class PermissionService : IPermissionService
{
    private readonly IProjectMemberRepository _projectMemberRepository;

    public PermissionService(IProjectMemberRepository projectMemberRepository)
    {
        _projectMemberRepository = projectMemberRepository;
    }

    public async Task<ProjectRole?> GetUserProjectRoleAsync(Guid userId, Guid projectId)
    {
        return await _projectMemberRepository.GetUserProjectRoleAsync(userId, projectId);
    }

    public async Task<bool> HasProjectAccessAsync(Guid userId, Guid projectId, ProjectRole minimumRole)
    {
        var userRole = await GetUserProjectRoleAsync(userId, projectId);

        if (userRole == null)
        {
            return false;
        }

        return userRole.Value >= minimumRole;
    }

    public async Task<bool> IsProjectOwnerAsync(Guid userId, Guid projectId)
    {
        var role = await GetUserProjectRoleAsync(userId, projectId);
        return role == ProjectRole.Owner;
    }

    public async Task<bool> IsProjectMemberAsync(Guid userId, Guid projectId)
    {
        return await _projectMemberRepository.ExistsAsync(userId, projectId);
    }

    public async Task<bool> CanManageProjectAsync(Guid userId, Guid projectId)
    {
        var role = await GetUserProjectRoleAsync(userId, projectId);
        return role == ProjectRole.Owner || role == ProjectRole.Editor;
    }
}
