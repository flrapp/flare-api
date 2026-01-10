using System.Text.RegularExpressions;
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

public class ScopeService : IScopeService
{
    private readonly IScopeRepository _scopeRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IProjectUserRepository _projectUserRepository;
    private readonly IPermissionService _permissionService;
    private readonly HybridCache _hybridCache;

    public ScopeService(
        IScopeRepository scopeRepository,
        IProjectRepository projectRepository,
        IProjectUserRepository projectUserRepository,
        IPermissionService permissionService,
        HybridCache hybridCache)
    {
        _scopeRepository = scopeRepository;
        _projectRepository = projectRepository;
        _projectUserRepository = projectUserRepository;
        _permissionService = permissionService;
        _hybridCache = hybridCache;
    }

    public async Task<ScopeResponseDto> CreateAsync(Guid projectId, CreateScopeDto dto, Guid currentUserId)
    {
        // Check permission
        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, projectId, ProjectPermission.ManageScopes))
        {
            throw new ForbiddenException("You do not have permission to create scopes in this project.");
        }

        // Verify project exists
        var projectExists = await _projectRepository.ExistsByIdAsync(projectId);
        if (!projectExists)
        {
            throw new NotFoundException("Project not found.");
        }
        
        var projectScopes = await _scopeRepository.GetByProjectIdAsync(projectId);
        var index = projectScopes.Any() ? projectScopes.MaxBy(x => x.Index)!.Index : 0;
        // Generate alias from name
        var alias = GenerateAlias(dto.Name);

        // Ensure alias is unique within project
        var counter = 1;
        var originalAlias = alias;
        while (await _scopeRepository.ExistsByProjectAndAliasAsync(projectId, alias))
        {
            alias = $"{originalAlias}-{counter}";
            counter++;
        }

        var scope = new Scope
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Alias = alias,
            Name = dto.Name,
            Description = dto.Description,
            CreatedAt = DateTime.UtcNow,
            Index = index
        };

        try
        {
            await _scopeRepository.AddAsync(scope);
            return MapToResponseDto(scope);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("duplicate") == true)
        {
            throw new BadRequestException("A scope with this name already exists in this project.");
        }
    }

    public async Task<ScopeResponseDto> UpdateAsync(Guid scopeId, UpdateScopeDto dto, Guid currentUserId)
    {
        var scope = await _scopeRepository.GetByIdWithProjectAsync(scopeId);
        if (scope == null)
        {
            throw new NotFoundException("Scope not found.");
        }

        // Check permission
        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, scope.ProjectId, ProjectPermission.ManageScopes))
        {
            throw new ForbiddenException("You do not have permission to update scopes in this project.");
        }

        var previousAlias = scope.Alias;
        scope.Alias = dto.Alias;
        scope.Name = dto.Name;
        scope.Description = dto.Description;

        try
        {
            await _scopeRepository.UpdateAsync(scope);
            var scopeTag = CacheKeys.ProjectScopeCacheTag(scope.Project.Alias, previousAlias);
            await _hybridCache.RemoveByTagAsync(scopeTag);
            return MapToResponseDto(scope);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("duplicate") == true)
        {
            throw new BadRequestException("A scope with this name already exists in this project.");
        }
    }

    public async Task DeleteAsync(Guid scopeId, Guid currentUserId)
    {
        var scope = await _scopeRepository.GetByIdWithProjectAsync(scopeId);
        if (scope == null)
        {
            throw new NotFoundException("Scope not found.");
        }

        // Check permission
        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, scope.ProjectId, ProjectPermission.ManageScopes))
        {
            throw new ForbiddenException("You do not have permission to delete scopes in this project.");
        }

        // Remove all scope permissions for this scope
        await _projectUserRepository.RemoveAllScopePermissionsForScopeAsync(scopeId);

        await _scopeRepository.DeleteAsync(scopeId);
        var scopeTag = CacheKeys.ProjectScopeCacheTag(scope.Project.Alias, scope.Alias);
        await _hybridCache.RemoveByTagAsync(scopeTag);
    }

    public async Task<ScopeResponseDto> GetByIdAsync(Guid scopeId, Guid currentUserId)
    {
        var scope = await _scopeRepository.GetByIdAsync(scopeId);
        if (scope == null)
        {
            throw new NotFoundException("Scope not found.");
        }

        // Check if user has access to the project
        if (!await _permissionService.IsProjectMemberAsync(currentUserId, scope.ProjectId))
        {
            throw new ForbiddenException("You do not have access to this scope.");
        }

        return MapToResponseDto(scope);
    }

    public async Task<List<ScopeResponseDto>> GetByProjectIdAsync(Guid projectId, Guid currentUserId)
    {
        // Check if user has access to the project
        if (!await _permissionService.IsProjectMemberAsync(currentUserId, projectId))
        {
            throw new ForbiddenException("You do not have access to this project.");
        }

        var scopes = await _scopeRepository.GetByProjectIdAsync(projectId);
        return scopes.Select(MapToResponseDto).ToList();
    }

    #region Helper Methods

    private static string GenerateAlias(string name)
    {
        // Convert to lowercase
        var alias = name.ToLowerInvariant();

        // Replace spaces and special characters with hyphens
        alias = Regex.Replace(alias, @"[^a-z0-9]+", "-");

        // Remove leading/trailing hyphens
        alias = alias.Trim('-');

        // Limit length to 100 characters
        if (alias.Length > 100)
        {
            alias = alias.Substring(0, 100).TrimEnd('-');
        }

        return alias;
    }

    private ScopeResponseDto MapToResponseDto(Scope scope)
    {
        return new ScopeResponseDto
        {
            Id = scope.Id,
            ProjectId = scope.ProjectId,
            Alias = scope.Alias,
            Name = scope.Name,
            Description = scope.Description,
            CreatedAt = scope.CreatedAt
        };
    }

    #endregion
}
