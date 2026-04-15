using Flare.Application.DTOs;

namespace Flare.Application.Interfaces;

public interface ITargetingRuleService
{
    Task<List<TargetingRuleDto>> GetRulesAsync(Guid flagValueId, Guid currentUserId);
    Task CreateRuleAsync(Guid flagValueId, CreateTargetingRuleDto dto, Guid currentUserId, string actorUsername);
    Task UpdateRuleAsync(Guid ruleId, UpdateTargetingRuleDto dto, Guid currentUserId, string actorUsername);
    Task DeleteRuleAsync(Guid ruleId, Guid currentUserId, string actorUsername);
    Task ReorderRulesAsync(Guid flagValueId, ReorderTargetingRulesDto dto, Guid currentUserId, string actorUsername);
    Task AddConditionAsync(Guid ruleId, CreateTargetingConditionDto dto, Guid currentUserId, string actorUsername);
    Task UpdateConditionAsync(Guid conditionId, UpdateTargetingConditionDto dto, Guid currentUserId,
        string actorUsername);
    Task DeleteConditionAsync(Guid conditionId, Guid currentUserId, string actorUsername);
}
