using Flare.Application.DTOs;
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
    private readonly IPermissionService _permissionService;
    private readonly HybridCache _hybridCache;

    public FeatureFlagService(
        IFeatureFlagRepository featureFlagRepository,
        IProjectRepository projectRepository,
        IScopeRepository scopeRepository,
        IPermissionService permissionService,
        HybridCache hybridCache)
    {
        _featureFlagRepository = featureFlagRepository;
        _projectRepository = projectRepository;
        _scopeRepository = scopeRepository;
        _permissionService = permissionService;
        _hybridCache = hybridCache;
    }

    public async Task<FeatureFlagResponseDto> CreateAsync(Guid projectId, CreateFeatureFlagDto dto, Guid currentUserId)
    {
        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, projectId, ProjectPermission.ManageFeatureFlags))
        {
            throw new ForbiddenException("You do not have permission to create feature flags in this project.");
        }

        var projectExists = await _projectRepository.ExistsByIdAsync(projectId);
        if (!projectExists)
        {
            throw new NotFoundException("Project not found.");
        }
        
        if(await _featureFlagRepository.ExistsByProjectAndKeyAsync(projectId, dto.Key))
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

            return await MapToResponseDtoAsync(featureFlag);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("duplicate") == true)
        {
            throw new BadRequestException("A feature flag with this name already exists in this project.");
        }
    }

    public async Task<FeatureFlagResponseDto> UpdateAsync(Guid featureFlagId, UpdateFeatureFlagDto dto, Guid currentUserId)
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
        
        if(await _featureFlagRepository.ExistsByProjectAndKeyAsync(featureFlag.ProjectId, featureFlag.Key))
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
            return await MapToResponseDtoAsync(featureFlag);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("duplicate") == true)
        {
            throw new BadRequestException("A feature flag with this name already exists in this project.");
        }
    }

    public async Task DeleteAsync(Guid featureFlagId, Guid currentUserId)
    {
        var featureFlag = await _featureFlagRepository.GetByIdWithScopesAndProjectAsync(featureFlagId);
        if (featureFlag == null)
        {
            throw new NotFoundException("Feature flag not found.");
        }

        // Check permission
        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, featureFlag.ProjectId, ProjectPermission.ManageFeatureFlags))
        {
            throw new ForbiddenException("You do not have permission to delete feature flags in this project.");
        }

        await _featureFlagRepository.DeleteAsync(featureFlagId);
        var projectFeatureFlagTag = CacheKeys.FeatureFlagProjectCacheTag(featureFlag.Project.Alias, featureFlag.Key);
        await _hybridCache.RemoveByTagAsync(projectFeatureFlagTag);
    }

    public async Task<List<FeatureFlagResponseDto>> GetByProjectIdAsync(Guid projectId, Guid currentUserId)
    {
        // Check if user has access to the project
        if (!await _permissionService.IsProjectMemberAsync(currentUserId, projectId))
        {
            throw new ForbiddenException("You do not have access to this project.");
        }

        var featureFlags = await _featureFlagRepository.GetByProjectIdAsync(projectId);

        var responseDtos = new List<FeatureFlagResponseDto>();
        foreach (var featureFlag in featureFlags)
        {
            // Load with values for each feature flag
            var featureFlagWithValues = await _featureFlagRepository.GetByIdWithValuesAsync(featureFlag.Id);
            if (featureFlagWithValues != null)
            {
                responseDtos.Add(await MapToResponseDtoAsync(featureFlagWithValues));
            }
        }

        return responseDtos;
    }

    public async Task<FeatureFlagValueDto> UpdateValueAsync(Guid featureFlagId, UpdateFeatureFlagValueDto dto, Guid currentUserId)
    {
        var featureFlag = await _featureFlagRepository.GetByIdWithScopesAndProjectAsync(featureFlagId);
        if (featureFlag == null)
        {
            throw new NotFoundException("Feature flag not found.");
        }

        // Check scope-level permission for specific scope
        if (!await _permissionService.HasScopePermissionAsync(currentUserId, dto.ScopeId, ScopePermission.UpdateFeatureFlags))
        {
            throw new ForbiddenException("You do not have permission to update feature flag values for this scope.");
        }

        // Verify scope belongs to the same project
        var scope = featureFlag.Project.Scopes.FirstOrDefault(s => s.Id == dto.ScopeId);
        if (scope == null)
        {
            throw new NotFoundException("Scope not found.");
        }

        if (scope.ProjectId != featureFlag.ProjectId)
        {
            throw new BadRequestException("Scope does not belong to the same project as the feature flag.");
        }

        // Find or create the feature flag value
        var featureFlagValue = await _featureFlagRepository.GetValueByFlagIdAndScopeIdAsync(featureFlag.Id, dto.ScopeId);

        if (featureFlagValue == null)
        {
            // Create new value if it doesn't exist
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
            // Update existing value
            featureFlagValue.IsEnabled = dto.IsEnabled;
            featureFlagValue.UpdatedAt = DateTime.UtcNow;
        }

        featureFlag.UpdatedAt = DateTime.UtcNow;
        await _featureFlagRepository.UpdateValueAsync(featureFlagValue);
        var cacheKey = CacheKeys.FeatureFlagCacheKey(featureFlag.Project.Alias, featureFlagValue.Scope.Alias, featureFlag.Key);
        await _hybridCache.RemoveAsync(cacheKey);

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

    #region Helper Methods

    private async Task<FeatureFlagResponseDto> MapToResponseDtoAsync(FeatureFlag featureFlag)
    {
        // Load values with scopes if not already loaded
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
                UpdatedAt = v.UpdatedAt
            }).ToList()
        };
    }

    #endregion
}
