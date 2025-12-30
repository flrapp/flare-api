using Flare.Domain.Entities;
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

    public async Task<ProjectUser?> GetByUserAndProjectAsync(Guid userId, Guid projectId)
    {
        return await _context.ProjectUsers
            .FirstOrDefaultAsync(pm => pm.UserId == userId && pm.ProjectId == projectId);
    }

    public async Task<bool> ExistsAsync(Guid userId, Guid projectId)
    {
        return await _context.ProjectUsers
            .AnyAsync(pm => pm.UserId == userId && pm.ProjectId == projectId);
    }
}
