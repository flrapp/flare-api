using Flare.Application.DTOs;
using Flare.Application.Interfaces;
using Flare.Domain.Entities;
using Flare.Domain.Enums;
using Flare.Domain.Exceptions;
using Flare.Infrastructure.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Flare.Application.Services;

public class ProjectUserService : IProjectUserService
{
    private readonly IProjectUserRepository _projectUserRepository;
    private readonly IUserRepository _userRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IScopeRepository _scopeRepository;
    private readonly IPermissionService _permissionService;

    public ProjectUserService(
        IProjectUserRepository projectUserRepository,
        IUserRepository userRepository,
        IProjectRepository projectRepository,
        IScopeRepository scopeRepository,
        IPermissionService permissionService)
    {
        _projectUserRepository = projectUserRepository;
        _userRepository = userRepository;
        _projectRepository = projectRepository;
        _scopeRepository = scopeRepository;
        _permissionService = permissionService;
    }

    public async Task<ProjectUserResponseDto> InviteUserAsync(Guid projectId, InviteUserDto dto, Guid inviterUserId)
    {
        // Check permission
        if (!await _permissionService.HasProjectPermissionAsync(inviterUserId, projectId, ProjectPermission.ManageUsers))
        {
            throw new ForbiddenException("You do not have permission to invite users to this project.");
        }

        // Verify project exists
        var projectExists = await _projectRepository.ExistsByIdAsync(projectId);
        if (!projectExists)
        {
            throw new NotFoundException("Project not found.");
        }

        // Verify user exists and is active
        var user = await _userRepository.GetByIdAsync(dto.UserId);
        if (user == null)
        {
            throw new NotFoundException("User not found.");
        }

        if (!user.IsActive)
        {
            throw new BadRequestException("User is not active.");
        }

        // Check if user is already a member
        if (await _projectUserRepository.ExistsAsync(dto.UserId, projectId))
        {
            throw new BadRequestException("User is already a member of this project.");
        }

        // Create ProjectUser
        var projectUser = new ProjectUser
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            UserId = dto.UserId,
            InvitedBy = inviterUserId,
            JoinedAt = DateTime.UtcNow
        };

        try
        {
            await _projectUserRepository.AddAsync(projectUser);
            return await MapToResponseDtoAsync(projectUser, user);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("duplicate") == true)
        {
            throw new BadRequestException("User is already a member of this project.");
        }
    }

    public async Task RemoveUserAsync(Guid projectId, Guid userId, Guid currentUserId)
    {
        // Check permission
        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, projectId, ProjectPermission.ManageUsers))
        {
            throw new ForbiddenException("You do not have permission to remove users from this project.");
        }

        var projectUser = await _projectUserRepository.GetByUserAndProjectAsync(userId, projectId);
        if (projectUser == null)
        {
            throw new NotFoundException("User is not a member of this project.");
        }

        // Prevent removing the last user with DeleteProject permission
        var usersWithDeletePermission = await _projectUserRepository.GetByProjectIdAsync(projectId);
        var usersWithDeleteCount = 0;
        foreach (var pu in usersWithDeletePermission)
        {
            if (await _projectUserRepository.HasProjectPermissionAsync(pu.UserId, projectId, ProjectPermission.DeleteProject))
            {
                usersWithDeleteCount++;
            }
        }

        if (usersWithDeleteCount == 1 && await _projectUserRepository.HasProjectPermissionAsync(userId, projectId, ProjectPermission.DeleteProject))
        {
            throw new BadRequestException("Cannot remove the only user with DeleteProject permission.");
        }

        await _projectUserRepository.DeleteAsync(projectUser.Id);
    }

    public async Task<List<ProjectUserResponseDto>> GetProjectUsersAsync(Guid projectId, Guid currentUserId)
    {
        // Check if user has access to the project
        if (!await _permissionService.IsProjectMemberAsync(currentUserId, projectId))
        {
            throw new ForbiddenException("You do not have access to this project.");
        }

        var projectUsers = await _projectUserRepository.GetByProjectIdAsync(projectId);

        var responseDtos = new List<ProjectUserResponseDto>();
        foreach (var projectUser in projectUsers)
        {
            var user = await _userRepository.GetByIdAsync(projectUser.UserId);
            if (user != null)
            {
                responseDtos.Add(await MapToResponseDtoAsync(projectUser, user));
            }
        }

        return responseDtos;
    }

    public async Task<ProjectUserResponseDto> GetProjectUserAsync(Guid projectId, Guid userId, Guid currentUserId)
    {
        // Check if user has access to the project
        if (!await _permissionService.IsProjectMemberAsync(currentUserId, projectId))
        {
            throw new ForbiddenException("You do not have access to this project.");
        }

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            throw new NotFoundException("User not found.");
        }
        
        var projectUser = await _projectUserRepository.GetByUserAndProjectAsync(userId, projectId);
        if (projectUser == null)
        {
            throw new NotFoundException("User is not a member of this project.");
        }

        return await MapToResponseDtoAsync(projectUser, user);
    }

    public async Task AssignProjectPermissionsAsync(Guid projectId, Guid userId, AssignProjectPermissionsDto dto, Guid currentUserId)
    {
        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, projectId, ProjectPermission.ManageUsers))
        {
            throw new ForbiddenException("You do not have permission to assign permissions in this project.");
        }

        var projectUser = await _projectUserRepository.GetByUserAndProjectAsync(userId, projectId);
        if (projectUser == null)
        {
            throw new NotFoundException("User is not a member of this project.");
        }

        foreach (var permission in dto.Permissions)
        {
            try
            {
                await _projectUserRepository.AddProjectPermissionAsync(projectUser.Id, permission);
            }
            catch (DbUpdateException)
            {
                // Permission already exists, ignore
            }
        }
    }

    public async Task RevokeProjectPermissionAsync(Guid projectId, Guid userId, ProjectPermission permission, Guid currentUserId)
    {
        // Check permission
        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, projectId, ProjectPermission.ManageUsers))
        {
            throw new ForbiddenException("You do not have permission to revoke permissions in this project.");
        }

        var projectUser = await _projectUserRepository.GetByUserAndProjectAsync(userId, projectId);
        if (projectUser == null)
        {
            throw new NotFoundException("User is not a member of this project.");
        }

        // If revoking DeleteProject permission, ensure at least one user still has it
        if (permission == ProjectPermission.DeleteProject)
        {
            var usersWithDeletePermission = await _projectUserRepository.GetByProjectIdAsync(projectId);
            var usersWithDeleteCount = 0;
            foreach (var pu in usersWithDeletePermission)
            {
                if (await _projectUserRepository.HasProjectPermissionAsync(pu.UserId, projectId, ProjectPermission.DeleteProject))
                {
                    usersWithDeleteCount++;
                }
            }

            if (usersWithDeleteCount <= 1)
            {
                throw new BadRequestException("Cannot revoke DeleteProject permission from the only user who has it.");
            }
        }

        await _projectUserRepository.RemoveProjectPermissionAsync(projectUser.Id, permission);
    }

    public async Task AssignScopePermissionsAsync(Guid projectId, Guid userId, AssignScopePermissionsDto dto, Guid currentUserId)
    {
        // Check permission
        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, projectId, ProjectPermission.ManageUsers))
        {
            throw new ForbiddenException("You do not have permission to assign permissions in this project.");
        }

        var projectUser = await _projectUserRepository.GetByUserAndProjectAsync(userId, projectId);
        if (projectUser == null)
        {
            throw new NotFoundException("User is not a member of this project.");
        }

        // Verify scope belongs to the project
        var scope = await _scopeRepository.GetByIdAsync(dto.ScopeId);
        if (scope == null)
        {
            throw new NotFoundException("Scope not found.");
        }

        if (scope.ProjectId != projectId)
        {
            throw new BadRequestException("Scope does not belong to this project.");
        }

        // Add each permission (repository should handle duplicates)
        foreach (var permission in dto.Permissions)
        {
            try
            {
                await _projectUserRepository.AddScopePermissionAsync(projectUser.Id, dto.ScopeId, permission);
            }
            catch (DbUpdateException)
            {
                // Permission already exists, ignore
            }
        }
    }

    public async Task RevokeScopePermissionAsync(Guid projectId, Guid userId, Guid scopeId, ScopePermission permission, Guid currentUserId)
    {
        // Check permission
        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, projectId, ProjectPermission.ManageUsers))
        {
            throw new ForbiddenException("You do not have permission to revoke permissions in this project.");
        }

        var projectUser = await _projectUserRepository.GetByUserAndProjectAsync(userId, projectId);
        if (projectUser == null)
        {
            throw new NotFoundException("User is not a member of this project.");
        }

        // Verify scope belongs to the project
        var scope = await _scopeRepository.GetByIdAsync(scopeId);
        if (scope == null)
        {
            throw new NotFoundException("Scope not found.");
        }

        if (scope.ProjectId != projectId)
        {
            throw new BadRequestException("Scope does not belong to this project.");
        }

        await _projectUserRepository.RemoveScopePermissionAsync(projectUser.Id, scopeId, permission);
    }

    #region Helper Methods

    private async Task<ProjectUserResponseDto> MapToResponseDtoAsync(ProjectUser projectUser, User user)
    {
        var projectPermissions = await _projectUserRepository.GetUserProjectPermissionsAsync(user.Id, projectUser.ProjectId);
        var scopePermissions = await _projectUserRepository.GetUserScopePermissionsAsync(user.Id, projectUser.ProjectId);

        return new ProjectUserResponseDto
        {
            Id = projectUser.Id,
            UserId = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            JoinedAt = projectUser.JoinedAt,
            ProjectPermissions = projectPermissions,
            ScopePermissions = scopePermissions
        };
    }

    #endregion
}
