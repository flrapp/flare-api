using Flare.Domain.Entities;
using Flare.Infrastructure.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Flare.Infrastructure.Data.Repositories.Implementation;

public class ScopeRepository : IScopeRepository
{
    private readonly ApplicationDbContext _context;

    public ScopeRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Scope?> GetByIdAsync(Guid scopeId)
    {
        return await _context.Scopes.FindAsync(scopeId);
    }

    public async Task<Scope?> GetByIdWithDetailsAsync(Guid scopeId)
    {
        return await _context.Scopes
            .Include(s => s.FeatureFlagValues)
                .ThenInclude(v => v.FeatureFlag)
            .FirstOrDefaultAsync(s => s.Id == scopeId);
    }

    public async Task<Scope?> GetByProjectAndAliasAsync(Guid projectId, string alias)
    {
        return await _context.Scopes
            .FirstOrDefaultAsync(s => s.ProjectId == projectId && s.Alias == alias);
    }

    public async Task<List<Scope>> GetByProjectIdAsync(Guid projectId)
    {
        return await _context.Scopes
            .Where(s => s.ProjectId == projectId)
            .OrderBy(s => s.Index)
            .ToListAsync();
    }

    public async Task<Scope> AddAsync(Scope scope)
    {
        _context.Scopes.Add(scope);
        await _context.SaveChangesAsync();
        return scope;
    }

    public async Task UpdateAsync(Scope scope)
    {
        _context.Scopes.Update(scope);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid scopeId)
    {
        var scope = await GetByIdAsync(scopeId);
        if (scope != null)
        {
            _context.Scopes.Remove(scope);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsByIdAsync(Guid scopeId)
    {
        return await _context.Scopes.AnyAsync(s => s.Id == scopeId);
    }

    public async Task<bool> ExistsByProjectAndAliasAsync(Guid projectId, string alias)
    {
        return await _context.Scopes
            .AnyAsync(s => s.ProjectId == projectId && s.Alias == alias);
    }

    public async Task<bool> ExistsByProjectAndAliasExcludingIdAsync(Guid projectId, string alias, Guid scopeId)
    {
        return await _context.Scopes
            .AnyAsync(s => s.ProjectId == projectId && s.Alias == alias && s.Id != scopeId);
    }
}
