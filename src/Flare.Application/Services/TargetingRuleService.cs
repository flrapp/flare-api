using System.Text.Json;
using Flare.Application.Audit;
using Flare.Application.DTOs;
using Flare.Application.Interfaces;
using Flare.Domain.Constants;
using Flare.Domain.Entities;
using Flare.Domain.Enums;
using Flare.Domain.Exceptions;
using Flare.Infrastructure.Data.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Hybrid;

namespace Flare.Application.Services;

public class TargetingRuleService : ITargetingRuleService
{
    private readonly ITargetingRuleRepository _targetingRuleRepository;
    private readonly IFeatureFlagRepository _featureFlagRepository;
    private readonly IPermissionService _permissionService;
    private readonly HybridCache _hybridCache;
    private readonly IAuditLogger _auditLogger;

    public TargetingRuleService(
        ITargetingRuleRepository targetingRuleRepository,
        IFeatureFlagRepository featureFlagRepository,
        IPermissionService permissionService,
        HybridCache hybridCache,
        IAuditLogger auditLogger)
    {
        _targetingRuleRepository = targetingRuleRepository;
        _featureFlagRepository = featureFlagRepository;
        _permissionService = permissionService;
        _hybridCache = hybridCache;
        _auditLogger = auditLogger;
    }

    public async Task<List<TargetingRuleDto>> GetRulesAsync(Guid flagValueId, Guid currentUserId)
    {
        var flagValue = await _featureFlagRepository.GetValueByIdWithNavigationsAsync(flagValueId);
        if (flagValue == null)
            throw new NotFoundException("Feature flag value not found.");

        if (!await _permissionService.HasScopePermissionAsync(currentUserId, flagValue.ScopeId, ScopePermission.ReadFeatureFlags))
            throw new ForbiddenException("You do not have permission to read feature flags for this scope.");

        var rules = await _targetingRuleRepository.GetByFlagValueIdAsync(flagValueId);
        return rules.Select(MapRuleToDto).ToList();
    }

    public async Task<TargetingRuleDto> CreateRuleAsync(Guid flagValueId, CreateTargetingRuleDto dto, Guid currentUserId, string actorUsername)
    {
        var flagValue = await _featureFlagRepository.GetValueByIdWithNavigationsAsync(flagValueId);
        if (flagValue == null)
            throw new NotFoundException("Feature flag value not found.");

        if (!await _permissionService.HasScopePermissionAsync(currentUserId, flagValue.ScopeId, ScopePermission.UpdateFeatureFlags))
            throw new ForbiddenException("You do not have permission to update feature flags for this scope.");

        foreach (var condition in dto.Conditions)
            ValidateConditionValue(condition.Operator, condition.Value);

        var nextPriority = await _targetingRuleRepository.CountByFlagValueIdAsync(flagValueId) + 1;

        var rule = new TargetingRule
        {
            Id = Guid.NewGuid(),
            FeatureFlagValueId = flagValueId,
            Priority = nextPriority,
            ServeValue = dto.ServeValue,
            Conditions = dto.Conditions.Select(c => new TargetingCondition
            {
                Id = Guid.NewGuid(),
                AttributeKey = c.AttributeKey,
                Operator = c.Operator,
                Value = c.Value
            }).ToList()
        };

        await _targetingRuleRepository.AddAsync(rule);
        await InvalidateFlagCacheAsync(flagValue);

        _auditLogger.LogProjectAudit(
            flagValue.FeatureFlag.Project.Alias, actorUsername,
            "TargetingRule", flagValue.Scope.Alias, "Created");

        return MapRuleToDto(rule);
    }

    public async Task<TargetingRuleDto> UpdateRuleAsync(Guid ruleId, UpdateTargetingRuleDto dto, Guid currentUserId, string actorUsername)
    {
        var rule = await _targetingRuleRepository.GetByIdWithConditionsAsync(ruleId);
        if (rule == null)
            throw new NotFoundException("Targeting rule not found.");

        var flagValue = rule.FeatureFlagValue;

        if (!await _permissionService.HasScopePermissionAsync(currentUserId, flagValue.ScopeId, ScopePermission.UpdateFeatureFlags))
            throw new ForbiddenException("You do not have permission to update feature flags for this scope.");

        if (dto.Priority != rule.Priority && await _targetingRuleRepository.PriorityExistsAsync(flagValue.Id, dto.Priority, excludeRuleId: ruleId))
            throw new BadRequestException($"Priority {dto.Priority} is already in use by another rule for this flag value.");

        rule.ServeValue = dto.ServeValue;
        rule.Priority = dto.Priority;

        await _targetingRuleRepository.UpdateAsync(rule);
        await InvalidateFlagCacheAsync(flagValue);

        _auditLogger.LogProjectAudit(
            flagValue.FeatureFlag.Project.Alias, actorUsername,
            "TargetingRule", flagValue.Scope.Alias, "Updated");

        return MapRuleToDto(rule);
    }

    public async Task DeleteRuleAsync(Guid ruleId, Guid currentUserId, string actorUsername)
    {
        var rule = await _targetingRuleRepository.GetByIdWithConditionsAsync(ruleId);
        if (rule == null)
            throw new NotFoundException("Targeting rule not found.");

        var flagValue = rule.FeatureFlagValue;

        if (!await _permissionService.HasScopePermissionAsync(currentUserId, flagValue.ScopeId, ScopePermission.UpdateFeatureFlags))
            throw new ForbiddenException("You do not have permission to update feature flags for this scope.");

        var deletedPriority = rule.Priority;
        await _targetingRuleRepository.DeleteAsync(ruleId);

        // Compact priorities: close the gap left by the deleted rule
        var remaining = await _targetingRuleRepository.GetByFlagValueIdAsync(flagValue.Id);
        var needsUpdate = remaining.Where(r => r.Priority > deletedPriority).ToList();
        foreach (var r in needsUpdate)
            r.Priority--;

        if (needsUpdate.Count > 0)
            await _targetingRuleRepository.UpdateRangeAsync(needsUpdate);

        await InvalidateFlagCacheAsync(flagValue);

        _auditLogger.LogProjectAudit(
            flagValue.FeatureFlag.Project.Alias, actorUsername,
            "TargetingRule", flagValue.Scope.Alias, "Deleted");
    }

    public async Task<List<TargetingRuleDto>> ReorderRulesAsync(Guid flagValueId, ReorderTargetingRulesDto dto, Guid currentUserId, string actorUsername)
    {
        var flagValue = await _featureFlagRepository.GetValueByIdWithNavigationsAsync(flagValueId);
        if (flagValue == null)
            throw new NotFoundException("Feature flag value not found.");

        if (!await _permissionService.HasScopePermissionAsync(currentUserId, flagValue.ScopeId, ScopePermission.UpdateFeatureFlags))
            throw new ForbiddenException("You do not have permission to update feature flags for this scope.");

        var existingRules = await _targetingRuleRepository.GetByFlagValueIdAsync(flagValueId);
        var existingIds = existingRules.Select(r => r.Id).ToHashSet();
        var requestedIds = dto.RuleIds.ToHashSet();

        if (!existingIds.SetEquals(requestedIds))
            throw new BadRequestException("The provided rule IDs must match exactly the existing rules for this flag value.");

        var ruleById = existingRules.ToDictionary(r => r.Id);
        for (var i = 0; i < dto.RuleIds.Count; i++)
            ruleById[dto.RuleIds[i]].Priority = i + 1;

        await _targetingRuleRepository.UpdateRangeAsync(existingRules);
        await InvalidateFlagCacheAsync(flagValue);

        _auditLogger.LogProjectAudit(
            flagValue.FeatureFlag.Project.Alias, actorUsername,
            "TargetingRule", flagValue.Scope.Alias, "Reordered");

        return existingRules.OrderBy(r => r.Priority).Select(MapRuleToDto).ToList();
    }

    public async Task<TargetingRuleDto> AddConditionAsync(Guid ruleId, CreateTargetingConditionDto dto, Guid currentUserId, string actorUsername)
    {
        var rule = await _targetingRuleRepository.GetByIdWithConditionsAsync(ruleId);
        if (rule == null)
            throw new NotFoundException("Targeting rule not found.");

        var flagValue = rule.FeatureFlagValue;

        if (!await _permissionService.HasScopePermissionAsync(currentUserId, flagValue.ScopeId, ScopePermission.UpdateFeatureFlags))
            throw new ForbiddenException("You do not have permission to update feature flags for this scope.");

        ValidateConditionValue(dto.Operator, dto.Value);

        var condition = new TargetingCondition
        {
            Id = Guid.NewGuid(),
            TargetingRuleId = ruleId,
            AttributeKey = dto.AttributeKey,
            Operator = dto.Operator,
            Value = dto.Value
        };

        await _targetingRuleRepository.AddConditionAsync(condition);
        rule.Conditions.Add(condition);

        await InvalidateFlagCacheAsync(flagValue);

        _auditLogger.LogProjectAudit(
            flagValue.FeatureFlag.Project.Alias, actorUsername,
            "TargetingCondition", flagValue.Scope.Alias, "Created");

        return MapRuleToDto(rule);
    }

    public async Task<TargetingRuleDto> UpdateConditionAsync(Guid conditionId, UpdateTargetingConditionDto dto, Guid currentUserId, string actorUsername)
    {
        var condition = await _targetingRuleRepository.GetConditionByIdAsync(conditionId);
        if (condition == null)
            throw new NotFoundException("Targeting condition not found.");

        var flagValue = condition.TargetingRule.FeatureFlagValue;

        if (!await _permissionService.HasScopePermissionAsync(currentUserId, flagValue.ScopeId, ScopePermission.UpdateFeatureFlags))
            throw new ForbiddenException("You do not have permission to update feature flags for this scope.");

        ValidateConditionValue(dto.Operator, dto.Value);

        condition.AttributeKey = dto.AttributeKey;
        condition.Operator = dto.Operator;
        condition.Value = dto.Value;

        await _targetingRuleRepository.UpdateConditionAsync(condition);
        await InvalidateFlagCacheAsync(flagValue);

        _auditLogger.LogProjectAudit(
            flagValue.FeatureFlag.Project.Alias, actorUsername,
            "TargetingCondition", flagValue.Scope.Alias, "Updated");

        return MapRuleToDto(condition.TargetingRule);
    }

    public async Task DeleteConditionAsync(Guid conditionId, Guid currentUserId, string actorUsername)
    {
        var condition = await _targetingRuleRepository.GetConditionByIdAsync(conditionId);
        if (condition == null)
            throw new NotFoundException("Targeting condition not found.");

        var rule = condition.TargetingRule;
        var flagValue = rule.FeatureFlagValue;

        if (!await _permissionService.HasScopePermissionAsync(currentUserId, flagValue.ScopeId, ScopePermission.UpdateFeatureFlags))
            throw new ForbiddenException("You do not have permission to update feature flags for this scope.");

        if (rule.Conditions.Count <= 1)
            throw new BadRequestException("A rule must have at least one condition.");

        await _targetingRuleRepository.DeleteConditionAsync(conditionId);
        await InvalidateFlagCacheAsync(flagValue);

        _auditLogger.LogProjectAudit(
            flagValue.FeatureFlag.Project.Alias, actorUsername,
            "TargetingCondition", flagValue.Scope.Alias, "Deleted");
    }

    #region Helpers

    private static void ValidateConditionValue(ComparisonOperator op, string value)
    {
        if (op is ComparisonOperator.In or ComparisonOperator.NotIn)
        {
            try
            {
                var doc = JsonDocument.Parse(value);
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                    throw new BadRequestException($"Value for operator '{op}' must be a JSON array (e.g. [\"a\",\"b\"]).");
            }
            catch (JsonException)
            {
                throw new BadRequestException($"Value for operator '{op}' must be a valid JSON array (e.g. [\"a\",\"b\"]).");
            }
        }

        if (op is ComparisonOperator.GreaterThan or ComparisonOperator.LessThan)
        {
            if (!decimal.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _))
                throw new BadRequestException($"Value for operator '{op}' must be a numeric value.");
        }
    }

    private async Task InvalidateFlagCacheAsync(Domain.Entities.FeatureFlagValue flagValue)
    {
        var cacheKey = CacheKeys.FeatureFlagCacheKey(
            flagValue.FeatureFlag.Project.Alias,
            flagValue.Scope.Alias,
            flagValue.FeatureFlag.Key);
        await _hybridCache.RemoveAsync(cacheKey);
    }

    private static TargetingRuleDto MapRuleToDto(TargetingRule rule)
    {
        return new TargetingRuleDto
        {
            Id = rule.Id,
            FeatureFlagValueId = rule.FeatureFlagValueId,
            Priority = rule.Priority,
            ServeValue = rule.ServeValue,
            Conditions = rule.Conditions
                .Select(c => new TargetingConditionDto
                {
                    Id = c.Id,
                    AttributeKey = c.AttributeKey,
                    Operator = c.Operator,
                    Value = c.Value
                })
                .ToList()
        };
    }

    #endregion
}
