using Flare.Domain.Entities;

namespace Flare.Infrastructure.Data.Repositories.Interfaces;

public interface IFeatureFlagRepository
{
    Task<FeatureFlag?> GetByIdAsync(Guid featureFlagId);
    Task<FeatureFlag?> GetByIdWithValuesAsync(Guid featureFlagId);
    Task<FeatureFlag?> GetByProjectAndKeyAsync(Guid projectId, string key);
    Task<List<FeatureFlag>> GetByProjectIdAsync(Guid projectId);
    Task<FeatureFlag> AddAsync(FeatureFlag featureFlag);
    Task UpdateAsync(FeatureFlag featureFlag);
    Task DeleteAsync(Guid featureFlagId);
    Task<bool> ExistsByIdAsync(Guid featureFlagId);
    Task<bool> ExistsByProjectAndKeyAsync(Guid projectId, string key);
    Task<bool> ExistsByProjectAndKeyExcludingIdAsync(Guid projectId, string key, Guid featureFlagId);

    Task<FeatureFlagValue?> GetByProjectScopeFlagAliasAsync(string projectAlias, string scopeAlias,
        string featureFlagKey);

    Task<FeatureFlag?> GetByIdWithScopesAndProjectAsync(Guid featureFlagId);
}
