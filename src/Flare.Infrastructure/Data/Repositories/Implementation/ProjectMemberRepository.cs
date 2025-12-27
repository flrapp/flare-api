using Flare.Domain.Entities;
using Flare.Domain.Enums;
using Flare.Infrastructure.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Flare.Infrastructure.Data.Repositories.Implementation;

public class ProjectMemberRepository : IProjectMemberRepository
{
    private readonly ApplicationDbContext _context;

    public ProjectMemberRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ProjectMember?> GetByUserAndProjectAsync(Guid userId, Guid projectId)
    {
        return await _context.ProjectMembers
            .FirstOrDefaultAsync(pm => pm.UserId == userId && pm.ProjectId == projectId);
    }

    public async Task<ProjectRole?> GetUserProjectRoleAsync(Guid userId, Guid projectId)
    {
        var membership = await GetByUserAndProjectAsync(userId, projectId);
        return membership?.ProjectRole;
    }

    public async Task<bool> ExistsAsync(Guid userId, Guid projectId)
    {
        return await _context.ProjectMembers
            .AnyAsync(pm => pm.UserId == userId && pm.ProjectId == projectId);
    }
}
