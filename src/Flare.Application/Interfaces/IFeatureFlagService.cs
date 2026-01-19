using Flare.Application.DTOs;
using Flare.Application.DTOs.Sdk;

namespace Flare.Application.Interfaces;

public interface IFeatureFlagService
{
    Task<FeatureFlagResponseDto> CreateAsync(Guid projectId, CreateFeatureFlagDto dto, Guid currentUserId);
    Task<FeatureFlagResponseDto> UpdateAsync(Guid featureFlagId, UpdateFeatureFlagDto dto, Guid currentUserId);
    Task DeleteAsync(Guid featureFlagId, Guid currentUserId);
    Task<List<FeatureFlagResponseDto>> GetByProjectIdAsync(Guid projectId, Guid currentUserId);
    Task<FeatureFlagValueDto> UpdateValueAsync(Guid featureFlagId, UpdateFeatureFlagValueDto dto, Guid currentUserId);

    Task<GetFeatureFlagValueDto> GetFeatureFlagValueAsync(string projectAlias, string scopeAlias,
        string featureFlagKey);

    /// <summary>
    /// Evaluates a feature flag using OpenFeature-compatible request/response format.
    /// </summary>
    Task<FlagEvaluationResponseDto> EvaluateFlagAsync(Guid projectId, string flagKey, EvaluationContextDto context);

    /// <summary>
    /// Evaluates all feature flags for a project and scope using OpenFeature-compatible format.
    /// </summary>
    Task<BulkEvaluationResponseDto> EvaluateAllFlagsAsync(Guid projectId, EvaluationContextDto context);
}
