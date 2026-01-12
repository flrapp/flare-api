using Flare.Domain.Entities;
using Flare.Infrastructure.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Flare.Infrastructure.Data.Repositories.Implementation;

public class ProjectRepository : IProjectRepository
{
    private readonly ApplicationDbContext _context;

    public ProjectRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Project?> GetByIdAsync(Guid projectId)
    {
        return await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
    }

    public async Task<Project?> GetByAliasAsync(string alias)
    {
        return await _context.Projects
            .Include(p => p.Scopes)
            .FirstOrDefaultAsync(p => p.Alias == alias);
    }

    public async Task<Project?> GetByApiKeyAsync(string apiKey)
    {
        return await _context.Projects
            .FirstOrDefaultAsync(p => p.ApiKey == apiKey);
    }

    public async Task<List<Project>> GetByUserIdAsync(Guid userId)
    {
        return await _context.Projects
            .Where(p => p.Members.Any(m => m.UserId == userId))
            .Where(p => !p.IsArchived)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Project>> GetAllAsync(bool includeArchived = false)
    {
        var query = _context.Projects.AsQueryable();

        if (!includeArchived)
        {
            query = query.Where(p => !p.IsArchived);
        }

        return await query.OrderByDescending(p => p.CreatedAt).ToListAsync();
    }

    public async Task<Project> AddAsync(Project project)
    {
        _context.Projects.Add(project);
        await _context.SaveChangesAsync();
        return project;
    }

    public async Task UpdateAsync(Project project)
    {
        _context.Projects.Update(project);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid projectId)
    {
        var project = await GetByIdAsync(projectId);
        if (project != null)
        {
            _context.Projects.Remove(project);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsByIdAsync(Guid projectId)
    {
        return await _context.Projects.AnyAsync(p => p.Id == projectId);
    }

    public async Task<bool> ExistsByAliasAsync(string alias)
    {
        return await _context.Projects.AnyAsync(p => p.Alias == alias);
    }

    public async Task<bool> ExistsByApiKeyAsync(string apiKey)
    {
        return await _context.Projects.AnyAsync(p => p.ApiKey == apiKey);
    }
}
