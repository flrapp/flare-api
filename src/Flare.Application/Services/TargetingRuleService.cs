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

        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, flagValue.FeatureFlag.ProjectId, ProjectPermission.ViewTargetingRules))
            throw new ForbiddenException("You do not have permission to view targeting rules for this project.");

        var rules = await _targetingRuleRepository.GetByFlagValueIdAsync(flagValueId);
        var type = flagValue.FeatureFlag.Type;
        return rules.Select(r => MapRuleToDto(r, type)).ToList();
    }

    public async Task CreateRuleAsync(Guid flagValueId, CreateTargetingRuleDto dto, Guid currentUserId, string actorUsername)
    {
        var flagValue = await _featureFlagRepository.GetValueByIdWithNavigationsAsync(flagValueId);
        if (flagValue == null)
            throw new NotFoundException("Feature flag value not found.");

        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, flagValue.FeatureFlag.ProjectId, ProjectPermission.ManageTargetingRules))
            throw new ForbiddenException("You do not have permission to manage targeting rules for this project.");

        foreach (var condition in dto.Conditions)
            ValidateConditionValue(condition.Operator, condition.Value);

        var nextPriority = await _targetingRuleRepository.CountByFlagValueIdAsync(flagValueId) + 1;

        var conditions = dto.Conditions.Select(c => new TargetingCondition
        {
            Id = Guid.NewGuid(),
            AttributeKey = c.AttributeKey,
            Operator = c.Operator,
            Value = c.Value
        }).ToList();

        var rule = dto.ServeValue.BuildRule(
            priority: nextPriority,
            flagValueId: flagValueId,
            parentType: flagValue.FeatureFlag.Type,
            conditions: conditions);

        await _targetingRuleRepository.AddAsync(rule);
        await InvalidateFlagCacheAsync(flagValue);

        _auditLogger.LogProjectAudit(
            flagValue.FeatureFlag.Project.Alias, actorUsername,
            "TargetingRule", flagValue.Scope.Alias, "Created");
    }

    public async Task UpdateRuleAsync(Guid ruleId, UpdateTargetingRuleDto dto, Guid currentUserId, string actorUsername)
    {
        var rule = await _targetingRuleRepository.GetByIdWithConditionsAsync(ruleId);
        if (rule == null)
            throw new NotFoundException("Targeting rule not found.");

        var flagValue = rule.FeatureFlagValue;

        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, flagValue.FeatureFlag.ProjectId, ProjectPermission.ManageTargetingRules))
            throw new ForbiddenException("You do not have permission to manage targeting rules for this project.");

        if (dto.Priority != rule.Priority && await _targetingRuleRepository.PriorityExistsAsync(flagValue.Id, dto.Priority, excludeRuleId: ruleId))
            throw new BadRequestException($"Priority {dto.Priority} is already in use by another rule for this flag value.");

        dto.ServeValue.ApplyTo(rule, flagValue.FeatureFlag.Type);
        rule.Priority = dto.Priority;

        await _targetingRuleRepository.UpdateAsync(rule);
        await InvalidateFlagCacheAsync(flagValue);

        _auditLogger.LogProjectAudit(
            flagValue.FeatureFlag.Project.Alias, actorUsername,
            "TargetingRule", flagValue.Scope.Alias, "Updated");
    }

    public async Task DeleteRuleAsync(Guid ruleId, Guid currentUserId, string actorUsername)
    {
        var rule = await _targetingRuleRepository.GetByIdWithConditionsAsync(ruleId);
        if (rule == null)
            throw new NotFoundException("Targeting rule not found.");

        var flagValue = rule.FeatureFlagValue;

        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, flagValue.FeatureFlag.ProjectId, ProjectPermission.ManageTargetingRules))
            throw new ForbiddenException("You do not have permission to manage targeting rules for this project.");

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

    public async Task ReorderRulesAsync(Guid flagValueId, ReorderTargetingRulesDto dto, Guid currentUserId, string actorUsername)
    {
        var flagValue = await _featureFlagRepository.GetValueByIdWithNavigationsAsync(flagValueId);
        if (flagValue == null)
            throw new NotFoundException("Feature flag value not found.");

        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, flagValue.FeatureFlag.ProjectId, ProjectPermission.ManageTargetingRules))
            throw new ForbiddenException("You do not have permission to manage targeting rules for this project.");

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
    }

    public async Task AddConditionAsync(Guid ruleId, CreateTargetingConditionDto dto, Guid currentUserId, string actorUsername)
    {
        var rule = await _targetingRuleRepository.GetByIdWithConditionsAsync(ruleId);
        if (rule == null)
            throw new NotFoundException("Targeting rule not found.");

        var flagValue = rule.FeatureFlagValue;

        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, flagValue.FeatureFlag.ProjectId, ProjectPermission.ManageTargetingRules))
            throw new ForbiddenException("You do not have permission to manage targeting rules for this project.");

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
    }

    public async Task UpdateConditionAsync(Guid conditionId, UpdateTargetingConditionDto dto, Guid currentUserId, string actorUsername)
    {
        var condition = await _targetingRuleRepository.GetConditionByIdAsync(conditionId);
        if (condition == null)
            throw new NotFoundException("Targeting condition not found.");

        var flagValue = condition.TargetingRule.FeatureFlagValue;

        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, flagValue.FeatureFlag.ProjectId, ProjectPermission.ManageTargetingRules))
            throw new ForbiddenException("You do not have permission to manage targeting rules for this project.");

        ValidateConditionValue(dto.Operator, dto.Value);

        condition.AttributeKey = dto.AttributeKey;
        condition.Operator = dto.Operator;
        condition.Value = dto.Value;

        await _targetingRuleRepository.UpdateConditionAsync(condition);
        await InvalidateFlagCacheAsync(flagValue);

        _auditLogger.LogProjectAudit(
            flagValue.FeatureFlag.Project.Alias, actorUsername,
            "TargetingCondition", flagValue.Scope.Alias, "Updated");
    }

    public async Task DeleteConditionAsync(Guid conditionId, Guid currentUserId, string actorUsername)
    {
        var condition = await _targetingRuleRepository.GetConditionByIdAsync(conditionId);
        if (condition == null)
            throw new NotFoundException("Targeting condition not found.");

        var rule = condition.TargetingRule;
        var flagValue = rule.FeatureFlagValue;

        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, flagValue.FeatureFlag.ProjectId, ProjectPermission.ManageTargetingRules))
            throw new ForbiddenException("You do not have permission to manage targeting rules for this project.");

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

        if (op is ComparisonOperator.InSegment or ComparisonOperator.NotInSegment)
        {
            if (!Guid.TryParse(value, out _))
                throw new BadRequestException($"Value for operator '{op}' must be a valid segment ID (UUID).");
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

    private static TargetingRuleDto MapRuleToDto(TargetingRule rule, FeatureFlagType type)
    {
        return new TargetingRuleDto
        {
            Id = rule.Id,
            FeatureFlagValueId = rule.FeatureFlagValueId,
            Priority = rule.Priority,
            ServeValue = FlagValueReader.ReadServe(rule, type),
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
