using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Flare.Application.DTOs;
using Flare.Application.Interfaces;
using Flare.Domain.Entities;
using Flare.Domain.Enums;
using Flare.Domain.Exceptions;
using Flare.Infrastructure.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Flare.Application.Services;

public class ProjectService : IProjectService
{
    private readonly IProjectRepository _projectRepository;
    private readonly IScopeRepository _scopeRepository;
    private readonly IProjectUserRepository _projectUserRepository;
    private readonly IPermissionService _permissionService;

    public ProjectService(
        IProjectRepository projectRepository,
        IScopeRepository scopeRepository,
        IProjectUserRepository projectUserRepository,
        IPermissionService permissionService)
    {
        _projectRepository = projectRepository;
        _scopeRepository = scopeRepository;
        _projectUserRepository = projectUserRepository;
        _permissionService = permissionService;
    }

    public async Task<ProjectDetailResponseDto> CreateAsync(CreateProjectDto dto, Guid creatorUserId)
    {
        // Generate alias from name
        var alias = GenerateAlias(dto.Name);

        // Ensure alias is unique
        var counter = 1;
        var originalAlias = alias;
        while (await _projectRepository.ExistsByAliasAsync(alias))
        {
            alias = $"{originalAlias}-{counter}";
            counter++;
        }

        // Generate API key
        var apiKey = GenerateApiKey();

        // Ensure API key is unique
        while (await _projectRepository.ExistsByApiKeyAsync(apiKey))
        {
            apiKey = GenerateApiKey();
        }

        // Create project
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Alias = alias,
            Name = dto.Name,
            Description = dto.Description,
            ApiKey = apiKey,
            CreatedBy = creatorUserId,
            IsArchived = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        try
        {
            await _projectRepository.AddAsync(project);

            var defaultScopes = new List<Scope>
            {
                new Scope
                {
                    Id = Guid.NewGuid(),
                    ProjectId = project.Id,
                    Alias = "dev",
                    Name = "Development",
                    Description = "Development environment",
                    CreatedAt = DateTime.UtcNow,
                    Index = 0
                },
                new Scope
                {
                    Id = Guid.NewGuid(),
                    ProjectId = project.Id,
                    Alias = "staging",
                    Name = "Staging",
                    Description = "Staging environment",
                    CreatedAt = DateTime.UtcNow,
                    Index = 1
                },
                new Scope
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

            foreach (var scope in defaultScopes)
            {
                await _scopeRepository.AddAsync(scope);
            }

            // Create ProjectUser for creator with ALL ProjectPermissions
            var projectUser = new ProjectUser
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                UserId = creatorUserId,
                InvitedBy = creatorUserId,
                JoinedAt = DateTime.UtcNow
            };

            await _projectUserRepository.AddAsync(projectUser);

            // Grant all project permissions to creator
            foreach (var permission in Enum.GetValues<ProjectPermission>())
            {
                await _projectUserRepository.AddProjectPermissionAsync(projectUser.Id, permission);
            }

            return await MapToDetailResponseDto(project, creatorUserId, true);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("duplicate") == true)
        {
            throw new BadRequestException("A project with this name already exists.");
        }
    }

    public async Task<ProjectDetailResponseDto> UpdateAsync(Guid projectId, UpdateProjectDto dto, Guid currentUserId)
    {
        // Check permission
        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, projectId, ProjectPermission.ManageProjectSettings))
        {
            throw new ForbiddenException("You do not have permission to update this project.");
        }

        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
        {
            throw new NotFoundException("Project not found.");
        }

        project.Name = dto.Name;
        project.Description = dto.Description;
        project.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _projectRepository.UpdateAsync(project);
            return await MapToDetailResponseDto(project, currentUserId, true);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("duplicate") == true)
        {
            throw new BadRequestException("A project with this name already exists.");
        }
    }

    public async Task DeleteAsync(Guid projectId, Guid currentUserId)
    {
        // Check permission
        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, projectId, ProjectPermission.DeleteProject))
        {
            throw new ForbiddenException("You do not have permission to delete this project.");
        }

        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
        {
            throw new NotFoundException("Project not found.");
        }

        await _projectRepository.DeleteAsync(projectId);
    }

    public async Task<ProjectDetailResponseDto> GetByIdAsync(Guid projectId, Guid currentUserId)
    {
        var project = await _projectRepository.GetByIdWithDetailsAsync(projectId);
        if (project == null)
        {
            throw new NotFoundException("Project not found.");
        }

        // Check if user has ViewApiKey permission
        var canViewApiKey = await _permissionService.HasProjectPermissionAsync(currentUserId, projectId, ProjectPermission.ViewApiKey);

        return await MapToDetailResponseDto(project, currentUserId, canViewApiKey);
    }

    public async Task<ProjectResponseDto> GetByAliasAsync(string alias, Guid currentUserId)
    {
        var project = await _projectRepository.GetByAliasAsync(alias);
        if (project == null)
        {
            throw new NotFoundException("Project not found.");
        }

        return MapToResponseDto(project);
    }

    public async Task<List<ProjectResponseDto>> GetUserProjectsAsync(Guid userId)
    {
        var projects = await _projectRepository.GetByUserIdAsync(userId);
        return projects.Select(MapToResponseDto).ToList();
    }

    public async Task<RegenerateApiKeyResponseDto> RegenerateApiKeyAsync(Guid projectId, Guid currentUserId)
    {
        // Check permission
        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, projectId, ProjectPermission.RegenerateApiKey))
        {
            throw new ForbiddenException("You do not have permission to regenerate the API key.");
        }

        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
        {
            throw new NotFoundException("Project not found.");
        }

        // Generate new API key
        var newApiKey = GenerateApiKey();

        // Ensure uniqueness
        while (await _projectRepository.ExistsByApiKeyAsync(newApiKey))
        {
            newApiKey = GenerateApiKey();
        }

        project.ApiKey = newApiKey;
        project.UpdatedAt = DateTime.UtcNow;

        await _projectRepository.UpdateAsync(project);

        return new RegenerateApiKeyResponseDto
        {
            ApiKey = newApiKey,
            RegeneratedAt = DateTime.UtcNow
        };
    }

    public async Task ArchiveAsync(Guid projectId, Guid currentUserId)
    {
        // Check permission
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
    }

    public async Task UnarchiveAsync(Guid projectId, Guid currentUserId)
    {
        // Check permission
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
    }

    public async Task<MyPermissionsResponseDto> GetMyPermissionsAsync(Guid projectId, Guid currentUserId)
    {
        // Check if project exists
        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
        {
            throw new NotFoundException("Project not found.");
        }

        // Check if user is a project member
        if (!await _permissionService.IsProjectMemberAsync(currentUserId, projectId))
        {
            throw new ForbiddenException("You are not a member of this project.");
        }

        // Get user's project and scope permissions
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

    private static string GenerateApiKey()
    {
        // Generate 32 random bytes
        var bytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }

        // Convert to base64 URL-safe string
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

    private async Task<ProjectDetailResponseDto> MapToDetailResponseDto(Project project, Guid currentUserId, bool canViewApiKey)
    {
        // Get the project with full details if not already loaded
        if (project.Members == null || project.Scopes == null || project.FeatureFlags == null)
        {
            project = await _projectRepository.GetByIdWithDetailsAsync(project.Id)
                ?? throw new NotFoundException("Project not found.");
        }

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
            MemberCount = project.Members?.Count ?? 0,
            ScopeCount = project.Scopes?.Count ?? 0,
            FeatureFlagCount = project.FeatureFlags?.Count ?? 0
        };
    }

    #endregion
}
