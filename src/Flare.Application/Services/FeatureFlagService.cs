using Flare.Application.Audit;
using Flare.Application.DTOs;
using Flare.Application.DTOs.Sdk;
using Flare.Application.Interfaces;
using Flare.Domain.Constants;
using Flare.Domain.Entities;
using Flare.Domain.Enums;
using Flare.Domain.Exceptions;
using Flare.Infrastructure.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Flare.Application.Services;

public class FeatureFlagService : IFeatureFlagService
{
    private readonly IFeatureFlagRepository _featureFlagRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IScopeRepository _scopeRepository;
    private readonly ISegmentRepository _segmentRepository;
    private readonly IPermissionService _permissionService;
    private readonly HybridCache _hybridCache;
    private readonly IAuditLogger _auditLogger;

    public FeatureFlagService(
        IFeatureFlagRepository featureFlagRepository,
        IProjectRepository projectRepository,
        IScopeRepository scopeRepository,
        ISegmentRepository segmentRepository,
        IPermissionService permissionService,
        HybridCache hybridCache,
        IAuditLogger auditLogger)
    {
        _featureFlagRepository = featureFlagRepository;
        _projectRepository = projectRepository;
        _scopeRepository = scopeRepository;
        _segmentRepository = segmentRepository;
        _permissionService = permissionService;
        _hybridCache = hybridCache;
        _auditLogger = auditLogger;
    }

    public async Task<FeatureFlagResponseDto> CreateAsync(Guid projectId, CreateFeatureFlagDto dto, Guid currentUserId, string actorUsername)
    {
        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, projectId, ProjectPermission.ManageFeatureFlags))
        {
            throw new ForbiddenException("You do not have permission to create feature flags in this project.");
        }

        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
        {
            throw new NotFoundException("Project not found.");
        }

        if (await _featureFlagRepository.ExistsByProjectAndKeyAsync(projectId, dto.Key))
            throw new InvalidOperationException("Feature flag with this key already exists in this project.");

        var featureFlag = new FeatureFlag
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Key = dto.Key,
            Name = dto.Name,
            Description = dto.Description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        try
        {
            var scopes = await _scopeRepository.GetByProjectIdAsync(projectId);
            foreach (var scope in scopes)
            {
                var featureFlagValue = new FeatureFlagValue
                {
                    Id = Guid.NewGuid(),
                    FeatureFlagId = featureFlag.Id,
                    ScopeId = scope.Id,
                    IsEnabled = false,
                    UpdatedAt = DateTime.UtcNow
                };

                featureFlag.Values.Add(featureFlagValue);
            }

            await _featureFlagRepository.AddAsync(featureFlag);

            _auditLogger.LogProjectAudit(project.Alias, actorUsername, "FeatureFlag", null, "Created");

            return await MapToResponseDtoAsync(featureFlag);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("duplicate") == true)
        {
            throw new BadRequestException("A feature flag with this name already exists in this project.");
        }
    }

    public async Task<FeatureFlagResponseDto> UpdateAsync(Guid featureFlagId, UpdateFeatureFlagDto dto, Guid currentUserId, string actorUsername)
    {
        var featureFlag = await _featureFlagRepository.GetByIdWithScopesAndProjectAsync(featureFlagId);
        if (featureFlag == null)
        {
            throw new NotFoundException("Feature flag not found.");
        }

        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, featureFlag.ProjectId, ProjectPermission.ManageFeatureFlags))
        {
            throw new ForbiddenException("You do not have permission to update feature flags in this project.");
        }

        if (await _featureFlagRepository.ExistsByProjectAndKeyAsync(featureFlag.ProjectId, featureFlag.Key))
            throw new InvalidOperationException("Feature flag with this key already exists in this project.");

        featureFlag.Name = dto.Name;
        featureFlag.Description = dto.Description;
        var previousKey = featureFlag.Key;
        featureFlag.Key = dto.Key;
        featureFlag.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _featureFlagRepository.UpdateAsync(featureFlag);
            var projectFeatureFlagTag = CacheKeys.FeatureFlagProjectCacheTag(featureFlag.Project.Alias, previousKey);
            await _hybridCache.RemoveByTagAsync(projectFeatureFlagTag);

            _auditLogger.LogProjectAudit(featureFlag.Project.Alias, actorUsername, "FeatureFlag", null, "Updated");

            return await MapToResponseDtoAsync(featureFlag);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("duplicate") == true)
        {
            throw new BadRequestException("A feature flag with this name already exists in this project.");
        }
    }

    public async Task DeleteAsync(Guid featureFlagId, Guid currentUserId, string actorUsername)
    {
        var featureFlag = await _featureFlagRepository.GetByIdWithScopesAndProjectAsync(featureFlagId);
        if (featureFlag == null)
        {
            throw new NotFoundException("Feature flag not found.");
        }

        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, featureFlag.ProjectId, ProjectPermission.ManageFeatureFlags))
        {
            throw new ForbiddenException("You do not have permission to delete feature flags in this project.");
        }

        await _featureFlagRepository.DeleteAsync(featureFlagId);
        var projectFeatureFlagTag = CacheKeys.FeatureFlagProjectCacheTag(featureFlag.Project.Alias, featureFlag.Key);
        await _hybridCache.RemoveByTagAsync(projectFeatureFlagTag);

        _auditLogger.LogProjectAudit(featureFlag.Project.Alias, actorUsername, "FeatureFlag", null, "Deleted");
    }

    public async Task<List<FeatureFlagResponseDto>> GetByProjectIdAsync(Guid projectId, Guid currentUserId)
    {
        if (!await _permissionService.IsProjectMemberAsync(currentUserId, projectId))
        {
            throw new ForbiddenException("You do not have access to this project.");
        }

        var featureFlags = await _featureFlagRepository.GetByProjectIdAsync(projectId);

        var responseDtos = new List<FeatureFlagResponseDto>();
        foreach (var featureFlag in featureFlags)
        {
            var featureFlagWithValues = await _featureFlagRepository.GetByIdWithValuesAsync(featureFlag.Id);
            if (featureFlagWithValues != null)
            {
                responseDtos.Add(await MapToResponseDtoAsync(featureFlagWithValues));
            }
        }

        return responseDtos;
    }

    public async Task<FeatureFlagValueDto> UpdateValueAsync(Guid featureFlagId, UpdateFeatureFlagValueDto dto, Guid currentUserId, string actorUsername)
    {
        var featureFlag = await _featureFlagRepository.GetByIdWithScopesAndProjectAsync(featureFlagId);
        if (featureFlag == null)
        {
            throw new NotFoundException("Feature flag not found.");
        }

        if (!await _permissionService.HasScopePermissionAsync(currentUserId, dto.ScopeId, ScopePermission.UpdateFeatureFlags))
        {
            throw new ForbiddenException("You do not have permission to update feature flag values for this scope.");
        }

        var scope = featureFlag.Project.Scopes.FirstOrDefault(s => s.Id == dto.ScopeId);
        if (scope == null)
        {
            throw new NotFoundException("Scope not found.");
        }

        if (scope.ProjectId != featureFlag.ProjectId)
        {
            throw new BadRequestException("Scope does not belong to the same project as the feature flag.");
        }

        var featureFlagValue = await _featureFlagRepository.GetValueByFlagIdAndScopeIdAsync(featureFlag.Id, dto.ScopeId);

        var previousIsEnabled = featureFlagValue?.IsEnabled ?? false;

        if (featureFlagValue == null)
        {
            featureFlagValue = new FeatureFlagValue
            {
                Id = Guid.NewGuid(),
                FeatureFlagId = featureFlagId,
                ScopeId = dto.ScopeId,
                IsEnabled = dto.IsEnabled,
                UpdatedAt = DateTime.UtcNow
            };

            featureFlag.Values.Add(featureFlagValue);
        }
        else
        {
            featureFlagValue.IsEnabled = dto.IsEnabled;
            featureFlagValue.UpdatedAt = DateTime.UtcNow;
        }

        featureFlag.UpdatedAt = DateTime.UtcNow;
        await _featureFlagRepository.UpdateValueAsync(featureFlagValue);
        var cacheKey = CacheKeys.FeatureFlagCacheKey(featureFlag.Project.Alias, featureFlagValue.Scope.Alias, featureFlag.Key);
        await _hybridCache.RemoveAsync(cacheKey);

        _auditLogger.LogProjectAudit(
            featureFlag.Project.Alias, actorUsername, "FeatureFlag", scope.Alias, "ValueUpdated",
            previousIsEnabled,
            dto.IsEnabled);

        return new FeatureFlagValueDto
        {
            Id = featureFlagValue.Id,
            ScopeId = scope.Id,
            ScopeName = scope.Name,
            ScopeAlias = scope.Alias,
            IsEnabled = featureFlagValue.IsEnabled,
            UpdatedAt = featureFlagValue.UpdatedAt
        };
    }

    public async Task<GetFeatureFlagValueDto> GetFeatureFlagValueAsync(string projectAlias, string scopeAlias,
        string featureFlagKey)
    {
        var cacheKey = CacheKeys.FeatureFlagCacheKey(projectAlias, scopeAlias, featureFlagKey);
        var projectFeatureFlagTag = CacheKeys.FeatureFlagProjectCacheTag(projectAlias, featureFlagKey);
        var projectScopeTag = CacheKeys.ProjectScopeCacheTag(projectAlias, scopeAlias);
        var result = await _hybridCache.GetOrCreateAsync(cacheKey,
            async _ =>
            {
                var result =
                    await _featureFlagRepository.GetByProjectScopeFlagAliasAsync(projectAlias, scopeAlias,
                        featureFlagKey);

                if (result == null)
                    throw new NotFoundException("Feature flag not found.");

                return new GetFeatureFlagValueDto
                {
                    Value = result.IsEnabled
                };
            },
            tags: [projectFeatureFlagTag, projectScopeTag]);
        return result;
    }

    public async Task<FlagEvaluationResponseDto> EvaluateFlagAsync(Guid projectId, string flagKey,
        EvaluationContextDto context)
    {
        var scopeAlias = context.Scope;

        var scope = await _scopeRepository.GetByProjectAndAliasAsync(projectId, scopeAlias);
        if (scope == null)
            throw new NotFoundException($"Scope '{scopeAlias}' not found in project.");

        var featureFlagValue = await _featureFlagRepository.GetByProjectIdScopeFlagKeyAsync(
            projectId, scopeAlias, flagKey);

        if (featureFlagValue == null)
            throw new NotFoundException($"Feature flag '{flagKey}' not found.");

        var (value, reason) = await EvaluateTargetingRulesAsync(featureFlagValue, context);
        return new FlagEvaluationResponseDto
        {
            FlagKey = flagKey,
            Value = value,
            Variant = value ? "enabled" : "disabled",
            Reason = reason,
        };
    }

    public async Task<BulkEvaluationResponseDto> EvaluateAllFlagsAsync(Guid projectId, EvaluationContextDto context)
    {
        var scopeAlias = context.Scope;

        var scope = await _scopeRepository.GetByProjectAndAliasAsync(projectId, scopeAlias);
        if (scope == null)
            throw new NotFoundException($"Scope '{scopeAlias}' not found in project.");

        var featureFlagValues = await _featureFlagRepository.GetAllByProjectIdAndScopeAliasAsync(projectId, scopeAlias);

        var flags = new List<FlagEvaluationResponseDto>();
        foreach (var ffv in featureFlagValues)
        {
            var (value, reason) = await EvaluateTargetingRulesAsync(ffv, context);
            flags.Add(new FlagEvaluationResponseDto
            {
                FlagKey = ffv.FeatureFlag.Key,
                Value = value,
                Variant = value ? "enabled" : "disabled",
                Reason = reason,
            });
        }

        return new BulkEvaluationResponseDto { Flags = flags };
    }

    private async Task<(bool value, string reason)> EvaluateTargetingRulesAsync(
        FeatureFlagValue flagValue, EvaluationContextDto context)
    {
        var rules = flagValue.TargetingRules.OrderBy(r => r.Priority).ToList();

        if (rules.Count == 0)
            return (flagValue.IsEnabled, "STATIC");

        foreach (var rule in rules)
        {
            if (await AllConditionsMatchAsync(rule.Conditions, context))
                return (rule.ServeValue, "TARGETING_MATCH");
        }

        return (flagValue.IsEnabled, "DEFAULT");
    }

    private async Task<bool> AllConditionsMatchAsync(
        IEnumerable<TargetingCondition> conditions, EvaluationContextDto context)
    {
        foreach (var condition in conditions)
        {
            if (!await EvaluateConditionAsync(condition, context))
                return false;
        }
        return true;
    }

    private async Task<bool> EvaluateConditionAsync(TargetingCondition condition, EvaluationContextDto context)
    {
        if (condition.Operator is ComparisonOperator.InSegment or ComparisonOperator.NotInSegment)
        {
            if (context.TargetingKey == null)
                return false;

            if (!Guid.TryParse(condition.Value, out var segmentId))
                return false;

            var inSegment = await _segmentRepository.IsTargetingKeyInSegmentAsync(segmentId, context.TargetingKey);
            return condition.Operator == ComparisonOperator.InSegment ? inSegment : !inSegment;
        }

        var attributeValue = ResolveAttributeValue(condition.AttributeKey, context);
        if (attributeValue == null)
            return false;

        return condition.Operator switch
        {
            ComparisonOperator.Equals => string.Equals(attributeValue, condition.Value, StringComparison.Ordinal),
            ComparisonOperator.NotEquals => !string.Equals(attributeValue, condition.Value, StringComparison.Ordinal),
            ComparisonOperator.Contains => attributeValue.Contains(condition.Value, StringComparison.Ordinal),
            ComparisonOperator.StartsWith => attributeValue.StartsWith(condition.Value, StringComparison.Ordinal),
            ComparisonOperator.EndsWith => attributeValue.EndsWith(condition.Value, StringComparison.Ordinal),
            ComparisonOperator.In => EvaluateInOperator(attributeValue, condition.Value),
            ComparisonOperator.NotIn => !EvaluateInOperator(attributeValue, condition.Value),
            ComparisonOperator.GreaterThan => EvaluateNumericComparison(attributeValue, condition.Value) > 0,
            ComparisonOperator.LessThan => EvaluateNumericComparison(attributeValue, condition.Value) < 0,
            _ => false
        };
    }

    private static string? ResolveAttributeValue(string attributeKey, EvaluationContextDto context)
    {
        if (attributeKey == "targetingKey")
            return context.TargetingKey;

        if (context.Attributes != null && context.Attributes.TryGetValue(attributeKey, out var element))
            return element.ValueKind == System.Text.Json.JsonValueKind.String
                ? element.GetString()
                : element.ToString();

        return null;
    }

    private static bool EvaluateInOperator(string attributeValue, string conditionValue)
    {
        try
        {
            var array = System.Text.Json.JsonSerializer.Deserialize<List<string>>(conditionValue);
            return array != null && array.Contains(attributeValue, StringComparer.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static int EvaluateNumericComparison(string attributeValue, string conditionValue)
    {
        if (decimal.TryParse(attributeValue, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var attr) &&
            decimal.TryParse(conditionValue, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var cond))
        {
            return attr.CompareTo(cond);
        }
        return 0;
    }

    #region Helper Methods

    private async Task<FeatureFlagResponseDto> MapToResponseDtoAsync(FeatureFlag featureFlag)
    {
        if (featureFlag.Values == null || !featureFlag.Values.Any() || featureFlag.Values.First().Scope == null)
        {
            featureFlag = await _featureFlagRepository.GetByIdWithValuesAsync(featureFlag.Id)
                ?? throw new NotFoundException("Feature flag not found.");
        }

        return new FeatureFlagResponseDto
        {
            Id = featureFlag.Id,
            ProjectId = featureFlag.ProjectId,
            Key = featureFlag.Key,
            Name = featureFlag.Name,
            Description = featureFlag.Description,
            CreatedAt = featureFlag.CreatedAt,
            UpdatedAt = featureFlag.UpdatedAt,
            Values = featureFlag.Values.Select(v => new FeatureFlagValueDto
            {
                Id = v.Id,
                ScopeId = v.ScopeId,
                ScopeName = v.Scope.Name,
                ScopeAlias = v.Scope.Alias,
                IsEnabled = v.IsEnabled,
                UpdatedAt = v.UpdatedAt,
                TargetingRules = v.TargetingRules
                    .OrderBy(r => r.Priority)
                    .Select(r => new TargetingRuleDto
                    {
                        Id = r.Id,
                        FeatureFlagValueId = r.FeatureFlagValueId,
                        Priority = r.Priority,
                        ServeValue = r.ServeValue,
                        Conditions = r.Conditions.Select(c => new TargetingConditionDto
                        {
                            Id = c.Id,
                            AttributeKey = c.AttributeKey,
                            Operator = c.Operator,
                            Value = c.Value
                        }).ToList()
                    }).ToList()
            }).ToList()
        };
    }

    #endregion
}
