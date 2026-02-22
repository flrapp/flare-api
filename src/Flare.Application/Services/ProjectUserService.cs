using Flare.Application.Audit;
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
    private readonly IAuditLogger _auditLogger;

    public ProjectUserService(
        IProjectUserRepository projectUserRepository,
        IUserRepository userRepository,
        IProjectRepository projectRepository,
        IScopeRepository scopeRepository,
        IPermissionService permissionService,
        IAuditLogger auditLogger)
    {
        _projectUserRepository = projectUserRepository;
        _userRepository = userRepository;
        _projectRepository = projectRepository;
        _scopeRepository = scopeRepository;
        _permissionService = permissionService;
        _auditLogger = auditLogger;
    }

    public async Task<ProjectUserResponseDto> InviteUserAsync(Guid projectId, InviteUserDto dto, Guid inviterUserId, string actorUsername)
    {
        if (!await _permissionService.HasProjectPermissionAsync(inviterUserId, projectId, ProjectPermission.ManageUsers))
        {
            throw new ForbiddenException("You do not have permission to invite users to this project.");
        }

        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
        {
            throw new NotFoundException("Project not found.");
        }

        var user = await _userRepository.GetByIdAsync(dto.UserId);
        if (user == null)
        {
            throw new NotFoundException("User not found.");
        }

        if (!user.IsActive)
        {
            throw new BadRequestException("User is not active.");
        }

        if (await _projectUserRepository.ExistsAsync(dto.UserId, projectId))
        {
            throw new BadRequestException("User is already a member of this project.");
        }

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

            foreach (var permission in dto.ProjectPermissions)
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

            foreach (var scopePermission in dto.ScopePermissions)
            {
                var scope = await _scopeRepository.GetByIdAsync(scopePermission.Key);
                if (scope != null && scope.ProjectId == projectId)
                {
                    foreach (var permission in scopePermission.Value)
                    {
                        try
                        {
                            await _projectUserRepository.AddScopePermissionAsync(projectUser.Id, scopePermission.Key, permission);
                        }
                        catch (DbUpdateException)
                        {
                            // Permission already exists, ignore
                        }
                    }
                }
            }

            _auditLogger.LogProjectAudit(project.Alias, actorUsername, "ProjectMember", null, "UserInvited");

            return await MapToResponseDtoAsync(projectUser, user);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("duplicate") == true)
        {
            throw new BadRequestException("User is already a member of this project.");
        }
    }

    public async Task RemoveUserAsync(Guid projectId, Guid userId, Guid currentUserId, string actorUsername)
    {
        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, projectId, ProjectPermission.ManageUsers))
        {
            throw new ForbiddenException("You do not have permission to remove users from this project.");
        }

        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
        {
            throw new NotFoundException("Project not found.");
        }

        var projectUser = await _projectUserRepository.GetByUserAndProjectAsync(userId, projectId);
        if (projectUser == null)
        {
            throw new NotFoundException("User is not a member of this project.");
        }

        var projectUsers = await _projectUserRepository.GetByProjectIdAsync(projectId);
        var usersWithManageUsersCount = 0;
        foreach (var pu in projectUsers)
        {
            if (await _projectUserRepository.HasProjectPermissionAsync(pu.UserId, projectId, ProjectPermission.ManageUsers))
            {
                usersWithManageUsersCount++;
            }
        }

        if (usersWithManageUsersCount == 1 && await _projectUserRepository.HasProjectPermissionAsync(userId, projectId, ProjectPermission.ManageUsers))
        {
            throw new BadRequestException("Cannot remove the only user with ManageUsers permission.");
        }

        var usersWithDeleteCount = 0;
        foreach (var pu in projectUsers)
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

        _auditLogger.LogProjectAudit(project.Alias, actorUsername, "ProjectMember", null, "UserRemoved");
    }

    public async Task<List<ProjectUserResponseDto>> GetProjectUsersAsync(Guid projectId, Guid currentUserId)
    {
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

    public async Task AssignProjectPermissionsAsync(Guid projectId, Guid userId, AssignProjectPermissionsDto dto, Guid currentUserId, string actorUsername)
    {
        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, projectId, ProjectPermission.ManageUsers))
        {
            throw new ForbiddenException("You do not have permission to assign permissions in this project.");
        }

        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
        {
            throw new NotFoundException("Project not found.");
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

        _auditLogger.LogProjectAudit(project.Alias, actorUsername, "ProjectMember", null, "ProjectPermissionsAssigned");
    }

    public async Task RevokeProjectPermissionAsync(Guid projectId, Guid userId, ProjectPermission permission, Guid currentUserId, string actorUsername)
    {
        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, projectId, ProjectPermission.ManageUsers))
        {
            throw new ForbiddenException("You do not have permission to revoke permissions in this project.");
        }

        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
        {
            throw new NotFoundException("Project not found.");
        }

        var projectUser = await _projectUserRepository.GetByUserAndProjectAsync(userId, projectId);
        if (projectUser == null)
        {
            throw new NotFoundException("User is not a member of this project.");
        }

        if (currentUserId == userId && permission == ProjectPermission.ManageUsers)
        {
            throw new BadRequestException("You cannot remove ManageUsers permission from yourself.");
        }

        if (permission == ProjectPermission.ManageUsers)
        {
            var usersWithManageUsersPermission = await _projectUserRepository.GetByProjectIdAsync(projectId);
            var usersWithManageUsersCount = 0;
            foreach (var pu in usersWithManageUsersPermission)
            {
                if (await _projectUserRepository.HasProjectPermissionAsync(pu.UserId, projectId, ProjectPermission.ManageUsers))
                {
                    usersWithManageUsersCount++;
                }
            }

            if (usersWithManageUsersCount <= 1)
            {
                throw new BadRequestException("Cannot revoke ManageUsers permission from the only user who has it.");
            }
        }

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

        _auditLogger.LogProjectAudit(project.Alias, actorUsername, "ProjectMember", null, "ProjectPermissionRevoked");
    }

    public async Task AssignScopePermissionsAsync(Guid projectId, Guid userId, AssignScopePermissionsDto dto, Guid currentUserId, string actorUsername)
    {
        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, projectId, ProjectPermission.ManageUsers))
        {
            throw new ForbiddenException("You do not have permission to assign permissions in this project.");
        }

        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
        {
            throw new NotFoundException("Project not found.");
        }

        var projectUser = await _projectUserRepository.GetByUserAndProjectAsync(userId, projectId);
        if (projectUser == null)
        {
            throw new NotFoundException("User is not a member of this project.");
        }

        var scope = await _scopeRepository.GetByIdAsync(dto.ScopeId);
        if (scope == null)
        {
            throw new NotFoundException("Scope not found.");
        }

        if (scope.ProjectId != projectId)
        {
            throw new BadRequestException("Scope does not belong to this project.");
        }

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

        _auditLogger.LogProjectAudit(project.Alias, actorUsername, "ProjectMember", scope.Alias, "ScopePermissionsAssigned");
    }

    public async Task RevokeScopePermissionAsync(Guid projectId, Guid userId, Guid scopeId, ScopePermission permission, Guid currentUserId, string actorUsername)
    {
        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, projectId, ProjectPermission.ManageUsers))
        {
            throw new ForbiddenException("You do not have permission to revoke permissions in this project.");
        }

        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
        {
            throw new NotFoundException("Project not found.");
        }

        var projectUser = await _projectUserRepository.GetByUserAndProjectAsync(userId, projectId);
        if (projectUser == null)
        {
            throw new NotFoundException("User is not a member of this project.");
        }

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

        _auditLogger.LogProjectAudit(project.Alias, actorUsername, "ProjectMember", scope.Alias, "ScopePermissionRevoked");
    }

    public async Task<ProjectUserResponseDto> UpdateUserPermissionsAsync(Guid projectId, Guid userId, UpdateUserPermissionsDto dto, Guid currentUserId, string actorUsername)
    {
        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, projectId, ProjectPermission.ManageUsers))
        {
            throw new ForbiddenException("You do not have permission to manage user permissions in this project.");
        }

        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
        {
            throw new NotFoundException("Project not found.");
        }

        var projectUser = await _projectUserRepository.GetByUserAndProjectWithPermissionsAsync(userId, projectId);
        if (projectUser == null)
        {
            throw new NotFoundException("User is not a member of this project.");
        }

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            throw new NotFoundException("User not found.");
        }

        if (currentUserId == userId && !dto.ProjectPermissions.Contains(ProjectPermission.ManageUsers))
        {
            throw new BadRequestException("You cannot remove ManageUsers permission from yourself.");
        }

        var currentProjectPermissions = await _projectUserRepository.GetUserProjectPermissionsAsync(userId, projectId);
        var currentScopePermissions = await _projectUserRepository.GetUserScopePermissionsAsync(userId, projectId);

        var oldValue = new
        {
            ProjectPermissions = currentProjectPermissions,
            ScopePermissions = currentScopePermissions
        };

        foreach (var permission in currentProjectPermissions)
        {
            await _projectUserRepository.RemoveProjectPermissionAsync(projectUser.Id, permission);
        }

        foreach (var scopeEntry in currentScopePermissions)
        {
            foreach (var permission in scopeEntry.Value)
            {
                await _projectUserRepository.RemoveScopePermissionAsync(projectUser.Id, scopeEntry.Key, permission);
            }
        }

        foreach (var permission in dto.ProjectPermissions)
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

        foreach (var scopePermission in dto.ScopePermissions)
        {
            var scope = await _scopeRepository.GetByIdAsync(scopePermission.Key);
            if (scope == null)
            {
                throw new NotFoundException($"Scope {scopePermission.Key} not found.");
            }

            if (scope.ProjectId != projectId)
            {
                throw new BadRequestException($"Scope {scopePermission.Key} does not belong to this project.");
            }

            foreach (var permission in scopePermission.Value)
            {
                try
                {
                    await _projectUserRepository.AddScopePermissionAsync(projectUser.Id, scopePermission.Key, permission);
                }
                catch (DbUpdateException)
                {
                    // Permission already exists, ignore
                }
            }
        }

        var newValue = new
        {
            ProjectPermissions = dto.ProjectPermissions,
            ScopePermissions = dto.ScopePermissions
        };

        _auditLogger.LogProjectAudit(project.Alias, actorUsername, "ProjectMember", null, "PermissionsUpdated", oldValue, newValue);

        return await MapToResponseDtoAsync(projectUser, user);
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
