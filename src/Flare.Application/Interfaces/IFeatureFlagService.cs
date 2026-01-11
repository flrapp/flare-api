using Flare.Application.DTOs;

namespace Flare.Application.Interfaces;

public interface IFeatureFlagService
{
    Task<FeatureFlagResponseDto> CreateAsync(Guid projectId, CreateFeatureFlagDto dto, Guid currentUserId);
    Task<FeatureFlagResponseDto> UpdateAsync(Guid featureFlagId, UpdateFeatureFlagDto dto, Guid currentUserId);
    Task DeleteAsync(Guid featureFlagId, Guid currentUserId);
    Task<FeatureFlagResponseDto> GetByIdAsync(Guid featureFlagId, Guid currentUserId);
    Task<List<FeatureFlagResponseDto>> GetByProjectIdAsync(Guid projectId, Guid currentUserId);
    Task<FeatureFlagValueDto> UpdateValueAsync(Guid featureFlagId, UpdateFeatureFlagValueDto dto, Guid currentUserId);

    Task<GetFeatureFlagValueDto> GetFeatureFlagValueAsync(string projectAlias, string scopeAlias,
        string featureFlagKey);
}
