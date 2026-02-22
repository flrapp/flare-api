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

public class ScopeService : IScopeService
{
    private readonly IScopeRepository _scopeRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IProjectUserRepository _projectUserRepository;
    private readonly IPermissionService _permissionService;
    private readonly HybridCache _hybridCache;
    private readonly IAuditLogger _auditLogger;

    public ScopeService(
        IScopeRepository scopeRepository,
        IProjectRepository projectRepository,
        IProjectUserRepository projectUserRepository,
        IPermissionService permissionService,
        HybridCache hybridCache,
        IAuditLogger auditLogger)
    {
        _scopeRepository = scopeRepository;
        _projectRepository = projectRepository;
        _projectUserRepository = projectUserRepository;
        _permissionService = permissionService;
        _hybridCache = hybridCache;
        _auditLogger = auditLogger;
    }

    public async Task<ScopeResponseDto> CreateAsync(Guid projectId, CreateScopeDto dto, Guid currentUserId, string actorUsername)
    {
        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, projectId, ProjectPermission.ManageScopes))
        {
            throw new ForbiddenException("You do not have permission to create scopes in this project.");
        }

        if (!await _scopeRepository.ExistsByProjectAndAliasAsync(projectId, dto.Alias))
        {
            throw new BadRequestException("Scope with this alias already exists.");
        }

        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
        {
            throw new NotFoundException("Project not found.");
        }

        var projectScopes = await _scopeRepository.GetByProjectIdAsync(projectId);
        var index = projectScopes.Any() ? projectScopes.MaxBy(x => x.Index)!.Index : 0;

        var scope = new Scope
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Alias = dto.Alias,
            Name = dto.Name,
            Description = dto.Description,
            CreatedAt = DateTime.UtcNow,
            Index = index
        };

        await _scopeRepository.AddAsync(scope);

        _auditLogger.LogProjectAudit(project.Alias, actorUsername, "Scope", dto.Alias, "Created");

        return MapToResponseDto(scope);
    }

    public async Task<ScopeResponseDto> UpdateAsync(Guid scopeId, UpdateScopeDto dto, Guid currentUserId, string actorUsername)
    {
        var scope = await _scopeRepository.GetByIdWithProjectAsync(scopeId);
        if (scope == null)
        {
            throw new NotFoundException("Scope not found.");
        }

        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, scope.ProjectId, ProjectPermission.ManageScopes))
        {
            throw new ForbiddenException("You do not have permission to update scopes in this project.");
        }

        var previousAlias = scope.Alias;
        scope.Alias = dto.Alias;
        scope.Name = dto.Name;
        scope.Description = dto.Description;

        await _scopeRepository.UpdateAsync(scope);
        var scopeTag = CacheKeys.ProjectScopeCacheTag(scope.Project.Alias, previousAlias);
        await _hybridCache.RemoveByTagAsync(scopeTag);

        _auditLogger.LogProjectAudit(scope.Project.Alias, actorUsername, "Scope", scope.Alias, "Updated");

        return MapToResponseDto(scope);
    }

    public async Task DeleteAsync(Guid scopeId, Guid currentUserId, string actorUsername)
    {
        var scope = await _scopeRepository.GetByIdWithProjectAsync(scopeId);
        if (scope == null)
        {
            throw new NotFoundException("Scope not found.");
        }

        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, scope.ProjectId, ProjectPermission.ManageScopes))
        {
            throw new ForbiddenException("You do not have permission to delete scopes in this project.");
        }

        await _projectUserRepository.RemoveAllScopePermissionsForScopeAsync(scopeId);

        await _scopeRepository.DeleteAsync(scopeId);
        var scopeTag = CacheKeys.ProjectScopeCacheTag(scope.Project.Alias, scope.Alias);
        await _hybridCache.RemoveByTagAsync(scopeTag);

        _auditLogger.LogProjectAudit(scope.Project.Alias, actorUsername, "Scope", scope.Alias, "Deleted");
    }

    public async Task<List<ScopeResponseDto>> GetByProjectIdAsync(Guid projectId, Guid currentUserId)
    {
        if (!await _permissionService.IsProjectMemberAsync(currentUserId, projectId))
        {
            throw new ForbiddenException("You do not have access to this project.");
        }

        var scopes = await _scopeRepository.GetByProjectIdAsync(projectId);
        return scopes.Select(MapToResponseDto).ToList();
    }

    #region Helper Methods

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
