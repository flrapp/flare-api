using Flare.Domain.Entities;
using Flare.Infrastructure.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Flare.Infrastructure.Data.Repositories.Implementation;

public class FeatureFlagRepository : IFeatureFlagRepository
{
    private readonly ApplicationDbContext _context;

    public FeatureFlagRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<FeatureFlag?> GetByIdAsync(Guid featureFlagId)
    {
        return await _context.FeatureFlags.FindAsync(featureFlagId);
    }

    public async Task<FeatureFlag?> GetByIdWithScopesAndProjectAsync(Guid featureFlagId)
    {
        return await _context.FeatureFlags
            .Include(ff => ff.Project)
            .ThenInclude(p => p.Scopes)
            .Where(f => f.Id == featureFlagId)
            .FirstOrDefaultAsync();
    }

    public async Task<FeatureFlagValue?> GetValueByFlagIdAndScopeIdAsync(Guid featureFlagId, Guid scopeId)
    {
        return await _context.FeatureFlagValues
            .FirstOrDefaultAsync(ffv => ffv.FeatureFlagId == featureFlagId && ffv.ScopeId == scopeId);
    }

    public async Task<FeatureFlag?> GetByIdWithValuesAsync(Guid featureFlagId)
    {
        return await _context.FeatureFlags
            .Include(f => f.Values)
                .ThenInclude(v => v.Scope)
            .FirstOrDefaultAsync(f => f.Id == featureFlagId);
    }

    public async Task<FeatureFlag?> GetByProjectAndKeyAsync(Guid projectId, string key)
    {
        return await _context.FeatureFlags
            .FirstOrDefaultAsync(f => f.ProjectId == projectId && f.Key == key);
    }

    public async Task<List<FeatureFlag>> GetByProjectIdAsync(Guid projectId)
    {
        return await _context.FeatureFlags
            .Where(f => f.ProjectId == projectId)
            .Include(f => f.Values)
                .ThenInclude(v => v.Scope)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();
    }

    public async Task<FeatureFlag> AddAsync(FeatureFlag featureFlag)
    {
        _context.FeatureFlags.Add(featureFlag);
        await _context.SaveChangesAsync();
        return featureFlag;
    }

    public async Task UpdateAsync(FeatureFlag featureFlag)
    {
        _context.FeatureFlags.Update(featureFlag);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateValueAsync(FeatureFlagValue featureFlagValue)
    {
        _context.FeatureFlagValues.Update(featureFlagValue);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid featureFlagId)
    {
        var featureFlag = await GetByIdAsync(featureFlagId);
        if (featureFlag != null)
        {
            _context.FeatureFlags.Remove(featureFlag);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsByIdAsync(Guid featureFlagId)
    {
        return await _context.FeatureFlags.AnyAsync(f => f.Id == featureFlagId);
    }

    public async Task<bool> ExistsByProjectAndKeyAsync(Guid projectId, string key)
    {
        return await _context.FeatureFlags
            .AnyAsync(f => f.ProjectId == projectId && f.Key == key);
    }

    public async Task<bool> ExistsByProjectAndKeyExcludingIdAsync(Guid projectId, string key, Guid featureFlagId)
    {
        return await _context.FeatureFlags
            .AnyAsync(f => f.ProjectId == projectId && f.Key == key && f.Id != featureFlagId);
    }

    public async Task<FeatureFlagValue?> GetByProjectScopeFlagAliasAsync(string projectAlias, string scopeAlias,
        string featureFlagKey)
    {
        return await _context.FeatureFlagValues
            .Where(ffv => ffv.FeatureFlag.Key == featureFlagKey && ffv.Scope.Alias == scopeAlias &&
                          ffv.FeatureFlag.Project.Alias == projectAlias)
            .FirstOrDefaultAsync();
    }

    public async Task<FeatureFlagValue?> GetByProjectIdScopeFlagKeyAsync(Guid projectId, string scopeAlias,
        string featureFlagKey)
    {
        return await _context.FeatureFlagValues
            .Include(ffv => ffv.Scope)
            .Where(ffv => ffv.FeatureFlag.Key == featureFlagKey &&
                          ffv.Scope.Alias == scopeAlias &&
                          ffv.FeatureFlag.ProjectId == projectId)
            .FirstOrDefaultAsync();
    }

    public async Task<List<FeatureFlagValue>> GetAllByProjectIdAndScopeAliasAsync(Guid projectId, string scopeAlias)
    {
        return await _context.FeatureFlagValues
            .Include(ffv => ffv.Scope)
            .Include(ffv => ffv.FeatureFlag)
            .Where(ffv => ffv.Scope.Alias == scopeAlias &&
                          ffv.FeatureFlag.ProjectId == projectId)
            .ToListAsync();
    }
}
