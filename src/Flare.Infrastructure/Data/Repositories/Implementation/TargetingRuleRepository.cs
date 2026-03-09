using Flare.Domain.Entities;
using Flare.Infrastructure.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Flare.Infrastructure.Data.Repositories.Implementation;

public class TargetingRuleRepository : ITargetingRuleRepository
{
    private readonly ApplicationDbContext _context;

    public TargetingRuleRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<TargetingRule>> GetByFlagValueIdAsync(Guid flagValueId)
    {
        return await _context.TargetingRules
            .Include(r => r.Conditions)
            .Where(r => r.FeatureFlagValueId == flagValueId)
            .OrderBy(r => r.Priority)
            .ToListAsync();
    }

    public async Task<TargetingRule?> GetByIdWithConditionsAsync(Guid ruleId)
    {
        return await _context.TargetingRules
            .Include(r => r.Conditions)
            .Include(r => r.FeatureFlagValue)
                .ThenInclude(v => v.Scope)
            .Include(r => r.FeatureFlagValue)
                .ThenInclude(v => v.FeatureFlag)
                    .ThenInclude(f => f.Project)
            .FirstOrDefaultAsync(r => r.Id == ruleId);
    }

    public async Task<bool> PriorityExistsAsync(Guid flagValueId, int priority, Guid? excludeRuleId = null)
    {
        var query = _context.TargetingRules
            .Where(r => r.FeatureFlagValueId == flagValueId && r.Priority == priority);

        if (excludeRuleId.HasValue)
            query = query.Where(r => r.Id != excludeRuleId.Value);

        return await query.AnyAsync();
    }

    public async Task<int> CountByFlagValueIdAsync(Guid flagValueId)
    {
        return await _context.TargetingRules
            .CountAsync(r => r.FeatureFlagValueId == flagValueId);
    }

    public async Task AddAsync(TargetingRule rule)
    {
        _context.TargetingRules.Add(rule);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(TargetingRule rule)
    {
        _context.TargetingRules.Update(rule);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateRangeAsync(IEnumerable<TargetingRule> rules)
    {
        _context.TargetingRules.UpdateRange(rules);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid ruleId)
    {
        var rule = await _context.TargetingRules.FindAsync(ruleId);
        if (rule != null)
        {
            _context.TargetingRules.Remove(rule);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<TargetingCondition?> GetConditionByIdAsync(Guid conditionId)
    {
        return await _context.TargetingConditions
            .Include(c => c.TargetingRule)
                .ThenInclude(r => r.Conditions)
            .Include(c => c.TargetingRule)
                .ThenInclude(r => r.FeatureFlagValue)
                    .ThenInclude(v => v.Scope)
            .Include(c => c.TargetingRule)
                .ThenInclude(r => r.FeatureFlagValue)
                    .ThenInclude(v => v.FeatureFlag)
                        .ThenInclude(f => f.Project)
            .FirstOrDefaultAsync(c => c.Id == conditionId);
    }

    public async Task AddConditionAsync(TargetingCondition condition)
    {
        _context.TargetingConditions.Add(condition);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateConditionAsync(TargetingCondition condition)
    {
        _context.TargetingConditions.Update(condition);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteConditionAsync(Guid conditionId)
    {
        var condition = await _context.TargetingConditions.FindAsync(conditionId);
        if (condition != null)
        {
            _context.TargetingConditions.Remove(condition);
            await _context.SaveChangesAsync();
        }
    }
}
