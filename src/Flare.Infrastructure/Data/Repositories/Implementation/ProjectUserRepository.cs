using Flare.Domain.Entities;
using Flare.Domain.Enums;
using Flare.Infrastructure.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Flare.Infrastructure.Data.Repositories.Implementation;

public class ProjectUserRepository : IProjectUserRepository
{
    private readonly ApplicationDbContext _context;

    public ProjectUserRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ProjectUser?> GetByIdAsync(Guid projectUserId)
    {
        return await _context.ProjectUsers.FindAsync(projectUserId);
    }

    public async Task<ProjectUser?> GetByUserAndProjectAsync(Guid userId, Guid projectId)
    {
        return await _context.ProjectUsers
            .Include(pu => pu.User)
            .FirstOrDefaultAsync(pu => pu.UserId == userId && pu.ProjectId == projectId);
    }

    public async Task<ProjectUser?> GetByUserAndProjectWithPermissionsAsync(Guid userId, Guid projectId)
    {
        return await _context.ProjectUsers
            .Include(pu => pu.User)
            .Include(pu => pu.ProjectPermissions)
            .Include(pu => pu.ScopePermissions)
            .FirstOrDefaultAsync(pu => pu.UserId == userId && pu.ProjectId == projectId);
    }

    public async Task<List<ProjectUser>> GetByProjectIdAsync(Guid projectId)
    {
        return await _context.ProjectUsers
            .Include(pu => pu.User)
            .Include(pu => pu.ProjectPermissions)
            .Include(pu => pu.ScopePermissions)
                .ThenInclude(sp => sp.Scope)
            .Where(pu => pu.ProjectId == projectId)
            .OrderBy(pu => pu.JoinedAt)
            .ToListAsync();
    }

    public async Task<ProjectUser> AddAsync(ProjectUser projectUser)
    {
        _context.ProjectUsers.Add(projectUser);
        await _context.SaveChangesAsync();
        return projectUser;
    }

    public async Task UpdateAsync(ProjectUser projectUser)
    {
        _context.ProjectUsers.Update(projectUser);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid projectUserId)
    {
        var projectUser = await GetByIdAsync(projectUserId);
        if (projectUser != null)
        {
            _context.ProjectUsers.Remove(projectUser);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<ProjectPermission>> GetUserProjectPermissionsAsync(Guid userId, Guid projectId)
    {
        return await _context.ProjectUserProjectPermissions
            .Where(p => p.ProjectUser.UserId == userId && p.ProjectUser.ProjectId == projectId)
            .Select(p => p.Permission)
            .Distinct()
            .ToListAsync();
    }

    public async Task<Dictionary<Guid, List<ScopePermission>>> GetUserScopePermissionsAsync(Guid userId, Guid projectId)
    {
        var permissions = await _context.ProjectUserScopePermissions
            .Where(p => p.ProjectUser.UserId == userId && p.ProjectUser.ProjectId == projectId)
            .Select(p => new { p.ScopeId, p.Permission })
            .ToListAsync();

        return permissions
            .GroupBy(p => p.ScopeId)
            .ToDictionary(g => g.Key, g => g.Select(p => p.Permission).ToList());
    }

    public async Task<bool> HasProjectPermissionAsync(Guid userId, Guid projectId, ProjectPermission permission)
    {
        return await _context.ProjectUserProjectPermissions
            .AnyAsync(p => p.ProjectUser.UserId == userId
                        && p.ProjectUser.ProjectId == projectId
                        && p.Permission == permission);
    }

    public async Task<bool> HasScopePermissionAsync(Guid userId, Guid scopeId, ScopePermission permission)
    {
        return await _context.ProjectUserScopePermissions
            .AnyAsync(p => p.ProjectUser.UserId == userId
                        && p.ScopeId == scopeId
                        && p.Permission == permission);
    }

    public async Task AddProjectPermissionAsync(Guid projectUserId, ProjectPermission permission)
    {
        var exists = await _context.ProjectUserProjectPermissions
            .AnyAsync(p => p.ProjectUserId == projectUserId && p.Permission == permission);

        if (!exists)
        {
            var projectPermission = new ProjectUserProjectPermission
            {
                Id = Guid.NewGuid(),
                ProjectUserId = projectUserId,
                Permission = permission
            };

            _context.ProjectUserProjectPermissions.Add(projectPermission);
            await _context.SaveChangesAsync();
        }
    }

    public async Task RemoveProjectPermissionAsync(Guid projectUserId, ProjectPermission permission)
    {
        var projectPermission = await _context.ProjectUserProjectPermissions
            .FirstOrDefaultAsync(p => p.ProjectUserId == projectUserId && p.Permission == permission);

        if (projectPermission != null)
        {
            _context.ProjectUserProjectPermissions.Remove(projectPermission);
            await _context.SaveChangesAsync();
        }
    }

    public async Task AddScopePermissionAsync(Guid projectUserId, Guid scopeId, ScopePermission permission)
    {
        var exists = await _context.ProjectUserScopePermissions
            .AnyAsync(p => p.ProjectUserId == projectUserId
                        && p.ScopeId == scopeId
                        && p.Permission == permission);

        if (!exists)
        {
            var scopePermission = new ProjectUserScopePermission
            {
                Id = Guid.NewGuid(),
                ProjectUserId = projectUserId,
                ScopeId = scopeId,
                Permission = permission
            };

            _context.ProjectUserScopePermissions.Add(scopePermission);
            await _context.SaveChangesAsync();
        }
    }

    public async Task RemoveScopePermissionAsync(Guid projectUserId, Guid scopeId, ScopePermission permission)
    {
        var scopePermission = await _context.ProjectUserScopePermissions
            .FirstOrDefaultAsync(p => p.ProjectUserId == projectUserId
                                   && p.ScopeId == scopeId
                                   && p.Permission == permission);

        if (scopePermission != null)
        {
            _context.ProjectUserScopePermissions.Remove(scopePermission);
            await _context.SaveChangesAsync();
        }
    }

    public async Task RemoveAllScopePermissionsForScopeAsync(Guid scopeId)
    {
        var permissions = await _context.ProjectUserScopePermissions
            .Where(p => p.ScopeId == scopeId)
            .ToListAsync();

        if (permissions.Any())
        {
            _context.ProjectUserScopePermissions.RemoveRange(permissions);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(Guid userId, Guid projectId)
    {
        return await _context.ProjectUsers
            .AnyAsync(pu => pu.UserId == userId && pu.ProjectId == projectId);
    }

    public async Task<bool> ExistsByIdAsync(Guid projectUserId)
    {
        return await _context.ProjectUsers.AnyAsync(pu => pu.Id == projectUserId);
    }
}
