using System.Security.Cryptography;
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

public class ProjectService : IProjectService
{
    private readonly IProjectRepository _projectRepository;
    private readonly IPermissionService _permissionService;
    private readonly HybridCache _hybridCache;
    private readonly IAuditLogger _auditLogger;

    public ProjectService(
        IProjectRepository projectRepository,
        IPermissionService permissionService,
        HybridCache hybridCache,
        IAuditLogger auditLogger)
    {
        _projectRepository = projectRepository;
        _permissionService = permissionService;
        _hybridCache = hybridCache;
        _auditLogger = auditLogger;
    }

    public async Task<ProjectDetailResponseDto> CreateAsync(CreateProjectDto dto, Guid creatorUserId, string actorUsername)
    {
        if (await _projectRepository.ExistsByAliasAsync(dto.Alias))
        {
           throw new BadRequestException("This alias already exists");
        }

        var apiKey = GenerateApiKey();

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Alias = dto.Alias,
            Name = dto.Name,
            Description = dto.Description,
            ApiKey = apiKey,
            CreatedBy = creatorUserId,
            IsArchived = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };


        var defaultScopes = new List<Scope>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                Alias = "dev",
                Name = "Development",
                Description = "Development environment",
                CreatedAt = DateTime.UtcNow,
                Index = 0
            },
            new()
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                Alias = "staging",
                Name = "Staging",
                Description = "Staging environment",
                CreatedAt = DateTime.UtcNow,
                Index = 1
            },
            new()
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                Alias = "production",
                Name = "Production",
                Description = "Production environment",
                CreatedAt = DateTime.UtcNow,
                Index = 2
            }
        };

        project.Scopes = defaultScopes;

        var projectUser = new ProjectUser
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            UserId = creatorUserId,
            InvitedBy = creatorUserId,
            JoinedAt = DateTime.UtcNow
        };

        projectUser.ProjectPermissions = Enum.GetValues<ProjectPermission>()
            .Select(x => new ProjectUserProjectPermission
                { Id = Guid.NewGuid(), Permission = x, ProjectUserId = projectUser.Id }).ToList();

        project.Members = [projectUser];

        await _projectRepository.AddAsync(project);

        _auditLogger.LogProjectAudit(project.Alias, actorUsername, "Project", null, "Created");

        return MapToDetailResponseDto(project, true);
    }

    public async Task<ProjectDetailResponseDto> UpdateAsync(Guid projectId, UpdateProjectDto dto, Guid currentUserId, string actorUsername)
    {
        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, projectId, ProjectPermission.ManageProjectSettings))
        {
            throw new ForbiddenException("You do not have permission to update this project.");
        }

        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
        {
            throw new NotFoundException("Project not found.");
        }

        var oldValue = new { project.Name, project.Alias, project.Description };

        var previousAlias = project.Alias;
        project.Alias = dto.Alias;
        project.Name = dto.Name;
        project.Description = dto.Description;
        project.UpdatedAt = DateTime.UtcNow;

        await _projectRepository.UpdateAsync(project);
        await _hybridCache.RemoveByTagAsync(CacheKeys.ProjectCacheTag(previousAlias));

        var newValue = new { dto.Name, dto.Alias, dto.Description };
        _auditLogger.LogProjectAudit(project.Alias, actorUsername, "Project", null, "Updated", oldValue, newValue);

        return MapToDetailResponseDto(project, true);
    }

    public async Task DeleteAsync(Guid projectId, Guid currentUserId, string actorUsername)
    {
        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, projectId, ProjectPermission.DeleteProject))
        {
            throw new ForbiddenException("You do not have permission to delete this project.");
        }

        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
        {
            throw new NotFoundException("Project not found.");
        }

        var projectAlias = project.Alias;

        await _projectRepository.DeleteAsync(projectId);
        await _hybridCache.RemoveByTagAsync(CacheKeys.ProjectCacheTag(projectAlias));

        _auditLogger.LogProjectAudit(projectAlias, actorUsername, "Project", null, "Deleted");
    }

    public async Task<ProjectDetailResponseDto> GetByIdAsync(Guid projectId, Guid currentUserId)
    {
        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
        {
            throw new NotFoundException("Project not found.");
        }

        var canViewApiKey = await _permissionService.HasProjectPermissionAsync(currentUserId, projectId, ProjectPermission.ViewApiKey);

        return MapToDetailResponseDto(project, canViewApiKey);
    }

    public async Task<List<ProjectResponseDto>> GetUserProjectsAsync(Guid userId)
    {
        var projects = await _projectRepository.GetByUserIdAsync(userId);
        return projects.Select(MapToResponseDto).ToList();
    }

    public async Task<RegenerateApiKeyResponseDto> RegenerateApiKeyAsync(Guid projectId, Guid currentUserId, string actorUsername)
    {
        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, projectId, ProjectPermission.RegenerateApiKey))
        {
            throw new ForbiddenException("You do not have permission to regenerate the API key.");
        }

        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
        {
            throw new NotFoundException("Project not found.");
        }

        var newApiKey = GenerateApiKey();

        project.ApiKey = newApiKey;
        project.UpdatedAt = DateTime.UtcNow;

        await _projectRepository.UpdateAsync(project);
        await _hybridCache.RemoveByTagAsync(CacheKeys.ProjectCacheTag(project.Alias));

        _auditLogger.LogProjectAudit(project.Alias, actorUsername, "Project", null, "ApiKeyRegenerated");

        return new RegenerateApiKeyResponseDto
        {
            ApiKey = newApiKey,
            RegeneratedAt = DateTime.UtcNow
        };
    }

    public async Task ArchiveAsync(Guid projectId, Guid currentUserId, string actorUsername)
    {
        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, projectId, ProjectPermission.ManageProjectSettings))
        {
            throw new ForbiddenException("You do not have permission to archive this project.");
        }

        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
        {
            throw new NotFoundException("Project not found.");
        }

        if (project.IsArchived)
        {
            throw new BadRequestException("Project is already archived.");
        }

        project.IsArchived = true;
        project.UpdatedAt = DateTime.UtcNow;

        await _projectRepository.UpdateAsync(project);

        _auditLogger.LogProjectAudit(project.Alias, actorUsername, "Project", null, "Archived");
    }

    public async Task UnarchiveAsync(Guid projectId, Guid currentUserId, string actorUsername)
    {
        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, projectId, ProjectPermission.ManageProjectSettings))
        {
            throw new ForbiddenException("You do not have permission to unarchive this project.");
        }

        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
        {
            throw new NotFoundException("Project not found.");
        }

        if (!project.IsArchived)
        {
            throw new BadRequestException("Project is not archived.");
        }

        project.IsArchived = false;
        project.UpdatedAt = DateTime.UtcNow;

        await _projectRepository.UpdateAsync(project);

        _auditLogger.LogProjectAudit(project.Alias, actorUsername, "Project", null, "Unarchived");
    }

    public async Task<MyPermissionsResponseDto> GetMyPermissionsAsync(Guid projectId, Guid currentUserId)
    {
        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
        {
            throw new NotFoundException("Project not found.");
        }

        if (!await _permissionService.IsProjectMemberAsync(currentUserId, projectId))
        {
            throw new ForbiddenException("You are not a member of this project.");
        }

        var projectPermissions = await _permissionService.GetUserProjectPermissionsAsync(currentUserId, projectId);
        var scopePermissions = await _permissionService.GetUserScopePermissionsAsync(currentUserId, projectId);

        return new MyPermissionsResponseDto
        {
            UserId = currentUserId,
            ProjectId = projectId,
            ProjectPermissions = projectPermissions,
            ScopePermissions = scopePermissions
        };
    }

    #region Helper Methods

    private static string GenerateApiKey()
    {
        var bytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }

        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private ProjectResponseDto MapToResponseDto(Project project)
    {
        return new ProjectResponseDto
        {
            Id = project.Id,
            Alias = project.Alias,
            Name = project.Name,
            Description = project.Description,
            CreatedBy = project.CreatedBy,
            IsArchived = project.IsArchived,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt
        };
    }

    private ProjectDetailResponseDto MapToDetailResponseDto(Project project, bool canViewApiKey)
    {
        return new ProjectDetailResponseDto
        {
            Id = project.Id,
            Alias = project.Alias,
            Name = project.Name,
            Description = project.Description,
            ApiKey = canViewApiKey ? project.ApiKey : null,
            CreatedBy = project.CreatedBy,
            IsArchived = project.IsArchived,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt,
        };
    }

    #endregion
}
