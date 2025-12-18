using Domian.Enums;
using Flare.Application.Interfaces;
using Flare.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Flare.Application.Services;

public class PermissionService : IPermissionService
{
    private readonly ApplicationDbContext _context;

    public PermissionService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ProjectRole?> GetUserProjectRoleAsync(Guid userId, Guid projectId)
    {
        var membership = await _context.ProjectMembers
            .FirstOrDefaultAsync(pm => pm.UserId == userId && pm.ProjectId == projectId);

        return membership?.ProjectRole;
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
        return await _context.ProjectMembers
            .AnyAsync(pm => pm.UserId == userId && pm.ProjectId == projectId);
    }

    public async Task<bool> CanManageProjectAsync(Guid userId, Guid projectId)
    {
        var role = await GetUserProjectRoleAsync(userId, projectId);
        return role == ProjectRole.Owner || role == ProjectRole.Editor;
    }
}
