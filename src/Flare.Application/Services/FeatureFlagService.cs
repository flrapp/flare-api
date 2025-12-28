using System.Text.RegularExpressions;
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
        // Check permission
        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, projectId, ProjectPermission.ManageFeatureFlags))
        {
            throw new ForbiddenException("You do not have permission to create feature flags in this project.");
        }

        // Verify project exists
        var projectExists = await _projectRepository.ExistsByIdAsync(projectId);
        if (!projectExists)
        {
            throw new NotFoundException("Project not found.");
        }

        // Generate key from name
        var key = GenerateKey(dto.Name);

        // Ensure key is unique within project
        var counter = 1;
        var originalKey = key;
        while (await _featureFlagRepository.ExistsByProjectAndKeyAsync(projectId, key))
        {
            key = $"{originalKey}_{counter}";
            counter++;
        }

        var featureFlag = new FeatureFlag
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Key = key,
            Name = dto.Name,
            Description = dto.Description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        try
        {
            await _featureFlagRepository.AddAsync(featureFlag);

            // Create FeatureFlagValue for ALL scopes in project with DefaultValue
            var scopes = await _scopeRepository.GetByProjectIdAsync(projectId);
            foreach (var scope in scopes)
            {
                var featureFlagValue = new FeatureFlagValue
                {
                    Id = Guid.NewGuid(),
                    FeatureFlagId = featureFlag.Id,
                    ScopeId = scope.Id,
                    IsEnabled = dto.DefaultValue,
                    UpdatedAt = DateTime.UtcNow
                };

                // Add the value to the feature flag's collection
                featureFlag.Values.Add(featureFlagValue);
            }

            // Save changes
            await _featureFlagRepository.UpdateAsync(featureFlag);

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

        // Check permission
        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, featureFlag.ProjectId, ProjectPermission.ManageFeatureFlags))
        {
            throw new ForbiddenException("You do not have permission to update feature flags in this project.");
        }

        // If name changed, regenerate key
        if (featureFlag.Name != dto.Name)
        {
            var key = GenerateKey(dto.Name);

            // Ensure key is unique within project (excluding current feature flag)
            var counter = 1;
            var originalKey = key;
            while (await _featureFlagRepository.ExistsByProjectAndKeyExcludingIdAsync(featureFlag.ProjectId, key, featureFlagId))
            {
                key = $"{originalKey}_{counter}";
                counter++;
            }

            featureFlag.Key = key;
        }

        featureFlag.Name = dto.Name;
        featureFlag.Description = dto.Description;
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

    private static string GenerateKey(string name)
    {
        // Convert to uppercase
        var key = name.ToUpperInvariant();

        // Replace spaces and special characters with underscores
        key = Regex.Replace(key, @"[^A-Z0-9]+", "_");

        // Remove leading/trailing underscores
        key = key.Trim('_');

        // Limit length to 100 characters
        if (key.Length > 100)
        {
            key = key.Substring(0, 100).TrimEnd('_');
        }

        return key;
    }

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
