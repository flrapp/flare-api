using Flare.Domain.Entities;

namespace Flare.Infrastructure.Data.Repositories.Interfaces;

public interface ITargetingRuleRepository
{
    Task<List<TargetingRule>> GetByFlagValueIdAsync(Guid flagValueId);
    Task<TargetingRule?> GetByIdWithConditionsAsync(Guid ruleId);
    Task<bool> PriorityExistsAsync(Guid flagValueId, int priority, Guid? excludeRuleId = null);
    Task<int> CountByFlagValueIdAsync(Guid flagValueId);
    Task AddAsync(TargetingRule rule);
    Task UpdateAsync(TargetingRule rule);
    Task UpdateRangeAsync(IEnumerable<TargetingRule> rules);
    Task DeleteAsync(Guid ruleId);

    Task<TargetingCondition?> GetConditionByIdAsync(Guid conditionId);
    Task AddConditionAsync(TargetingCondition condition);
    Task UpdateConditionAsync(TargetingCondition condition);
    Task DeleteConditionAsync(Guid conditionId);
}
