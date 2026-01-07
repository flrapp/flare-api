using Flare.Application.DTOs;
using Flare.Application.Interfaces;
using Flare.Domain.Entities;
using Flare.Domain.Enums;
using Flare.Domain.Exceptions;
using Flare.Infrastructure.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Flare.Application.Services;

public class FeatureFlagService : IFeatureFlagService
{
    private readonly IFeatureFlagRepository _featureFlagRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IScopeRepository _scopeRepository;
    private readonly IPermissionService _permissionService;

    public FeatureFlagService(
        IFeatureFlagRepository featureFlagRepository,
        IProjectRepository projectRepository,
        IScopeRepository scopeRepository,
        IPermissionService permissionService)
    {
        _featureFlagRepository = featureFlagRepository;
        _projectRepository = projectRepository;
        _scopeRepository = scopeRepository;
        _permissionService = permissionService;
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
        var featureFlag = await _featureFlagRepository.GetByIdAsync(featureFlagId);
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
        featureFlag.Key = dto.Key;
        featureFlag.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _featureFlagRepository.UpdateAsync(featureFlag);
            return await MapToResponseDtoAsync(featureFlag);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("duplicate") == true)
        {
            throw new BadRequestException("A feature flag with this name already exists in this project.");
        }
    }

    public async Task DeleteAsync(Guid featureFlagId, Guid currentUserId)
    {
        var featureFlag = await _featureFlagRepository.GetByIdAsync(featureFlagId);
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
    }

    public async Task<FeatureFlagResponseDto> GetByIdAsync(Guid featureFlagId, Guid currentUserId)
    {
        var featureFlag = await _featureFlagRepository.GetByIdWithValuesAsync(featureFlagId);
        if (featureFlag == null)
        {
            throw new NotFoundException("Feature flag not found.");
        }

        // Check if user has access to the project
        if (!await _permissionService.IsProjectMemberAsync(currentUserId, featureFlag.ProjectId))
        {
            throw new ForbiddenException("You do not have access to this feature flag.");
        }

        return await MapToResponseDtoAsync(featureFlag);
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
        var featureFlag = await _featureFlagRepository.GetByIdWithValuesAsync(featureFlagId);
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
        var scope = await _scopeRepository.GetByIdAsync(dto.ScopeId);
        if (scope == null)
        {
            throw new NotFoundException("Scope not found.");
        }

        if (scope.ProjectId != featureFlag.ProjectId)
        {
            throw new BadRequestException("Scope does not belong to the same project as the feature flag.");
        }

        // Find or create the feature flag value
        var featureFlagValue = featureFlag.Values.FirstOrDefault(v => v.ScopeId == dto.ScopeId);

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
        await _featureFlagRepository.UpdateAsync(featureFlag);

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
