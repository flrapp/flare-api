using Flare.Application.DTOs;

namespace Flare.Application.Interfaces;

public interface ITargetingRuleService
{
    Task<List<TargetingRuleDto>> GetRulesAsync(Guid flagValueId, Guid currentUserId);
    Task<TargetingRuleDto> CreateRuleAsync(Guid flagValueId, CreateTargetingRuleDto dto, Guid currentUserId, string actorUsername);
    Task<TargetingRuleDto> UpdateRuleAsync(Guid ruleId, UpdateTargetingRuleDto dto, Guid currentUserId, string actorUsername);
    Task DeleteRuleAsync(Guid ruleId, Guid currentUserId, string actorUsername);
    Task<List<TargetingRuleDto>> ReorderRulesAsync(Guid flagValueId, ReorderTargetingRulesDto dto, Guid currentUserId, string actorUsername);
    Task<TargetingRuleDto> AddConditionAsync(Guid ruleId, CreateTargetingConditionDto dto, Guid currentUserId, string actorUsername);
    Task<TargetingRuleDto> UpdateConditionAsync(Guid conditionId, UpdateTargetingConditionDto dto, Guid currentUserId, string actorUsername);
    Task DeleteConditionAsync(Guid conditionId, Guid currentUserId, string actorUsername);
}
