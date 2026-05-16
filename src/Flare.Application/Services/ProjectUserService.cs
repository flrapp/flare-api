using Flare.Application.Audit;
using Flare.Application.DTOs;
using Flare.Application.Interfaces;
using Flare.Domain.Entities;
using Flare.Domain.Enums;
using Flare.Domain.Exceptions;
using Flare.Infrastructure.Data.Repositories.Interfaces;

namespace Flare.Application.Services;

public class ProjectUserService : IProjectUserService
{
    private readonly IProjectUserRepository _projectUserRepository;
    private readonly IUserRepository _userRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IScopeRepository _scopeRepository;
    private readonly IPermissionService _permissionService;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;

    public ProjectUserService(
        IProjectUserRepository projectUserRepository,
        IUserRepository userRepository,
        IProjectRepository projectRepository,
        IScopeRepository scopeRepository,
        IPermissionService permissionService,
        IAuditLogger auditLogger,
        IUnitOfWork unitOfWork)
    {
        _projectUserRepository = projectUserRepository;
        _userRepository = userRepository;
        _projectRepository = projectRepository;
        _scopeRepository = scopeRepository;
        _permissionService = permissionService;
        _auditLogger = auditLogger;
        _unitOfWork = unitOfWork;
    }

    public async Task<ProjectUserResponseDto> InviteUserAsync(Guid projectId, InviteUserDto dto, Guid inviterUserId,
        string actorUsername)
    {
        if (!await _permissionService.HasProjectPermissionAsync(inviterUserId, projectId,
                ProjectPermission.ManageUsers))
        {
            throw new ForbiddenException("You do not have permission to invite users to this project.");
        }

        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
            throw new NotFoundException("Project not found.");

        var user = await _userRepository.GetByIdAsync(dto.UserId);
        if (user == null)
            throw new NotFoundException("User not found.");

        if (!user.IsActive)
            throw new BadRequestException("User is not active.");

        if (await _projectUserRepository.ExistsAsync(dto.UserId, projectId))
            throw new BadRequestException("User is already a member of this project.");

        var validScopePermissions = await ValidateScopePermissionsAsync(dto.ScopePermissions, projectId);

        var projectUser = new ProjectUser
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            UserId = dto.UserId,
            InvitedBy = inviterUserId,
            JoinedAt = DateTime.UtcNow
        };

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            await _projectUserRepository.AddAsync(projectUser);
            await _projectUserRepository.AddProjectPermissionsAsync(projectUser.Id, dto.ProjectPermissions);

            if (validScopePermissions.Count > 0)
                await _projectUserRepository.AddScopePermissionsAsync(projectUser.Id, validScopePermissions);

            await _unitOfWork.CommitAsync();
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }

        _auditLogger.LogProjectAudit(project.Alias, actorUsername, "ProjectMember", null, "UserInvited");

        return await MapToResponseDtoAsync(projectUser, user);
    }

    public async Task RemoveUserAsync(Guid projectId, Guid userId, Guid currentUserId, string actorUsername)
    {
        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, projectId, ProjectPermission.ManageUsers))
            throw new ForbiddenException("You do not have permission to remove users from this project.");

        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
            throw new NotFoundException("Project not found.");

        var projectUser = await _projectUserRepository.GetByUserAndProjectAsync(userId, projectId);
        if (projectUser == null)
            throw new NotFoundException("User is not a member of this project.");

        var projectUsers = await _projectUserRepository.GetByProjectIdAsync(projectId);
        var usersWithManageUsersCount = 0;
        foreach (var pu in projectUsers)
        {
            if (await _projectUserRepository.HasProjectPermissionAsync(pu.UserId, projectId, ProjectPermission.ManageUsers))
                usersWithManageUsersCount++;
        }

        if (usersWithManageUsersCount == 1 && await _projectUserRepository.HasProjectPermissionAsync(userId, projectId, ProjectPermission.ManageUsers))
            throw new BadRequestException("Cannot remove the only user with ManageUsers permission.");

        var usersWithDeleteCount = 0;
        foreach (var pu in projectUsers)
        {
            if (await _projectUserRepository.HasProjectPermissionAsync(pu.UserId, projectId, ProjectPermission.DeleteProject))
                usersWithDeleteCount++;
        }

        if (usersWithDeleteCount == 1 && await _projectUserRepository.HasProjectPermissionAsync(userId, projectId, ProjectPermission.DeleteProject))
            throw new BadRequestException("Cannot remove the only user with DeleteProject permission.");

        await _projectUserRepository.DeleteAsync(projectUser.Id);

        _auditLogger.LogProjectAudit(project.Alias, actorUsername, "ProjectMember", null, "UserRemoved");
    }

    public async Task<PagedResult<ProjectUserResponseDto>> GetProjectUsersAsync(Guid projectId, Guid currentUserId, string? search = null, int page = 1, int pageSize = 20)
    {
        if (!await _permissionService.IsProjectMemberAsync(currentUserId, projectId))
            throw new ForbiddenException("You do not have access to this project.");

        var projectUsers = await _projectUserRepository.GetByProjectIdAsync(projectId);

        var responseDtos = new List<ProjectUserResponseDto>();
        foreach (var projectUser in projectUsers)
        {
            var user = await _userRepository.GetByIdAsync(projectUser.UserId);
            if (user != null)
                responseDtos.Add(await MapToResponseDtoAsync(projectUser, user));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            responseDtos = responseDtos.Where(u =>
                u.Username.ToLower().Contains(term) ||
                u.FullName.ToLower().Contains(term)).ToList();
        }

        var totalCount = responseDtos.Count;
        var items = responseDtos
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResult<ProjectUserResponseDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ProjectUserResponseDto> UpdateUserPermissionsAsync(Guid projectId, Guid userId, UpdateUserPermissionsDto dto, Guid currentUserId, string actorUsername)
    {
        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, projectId, ProjectPermission.ManageUsers))
            throw new ForbiddenException("You do not have permission to manage user permissions in this project.");

        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
            throw new NotFoundException("Project not found.");

        var projectUser = await _projectUserRepository.GetByUserAndProjectWithPermissionsAsync(userId, projectId);
        if (projectUser == null)
            throw new NotFoundException("User is not a member of this project.");

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            throw new NotFoundException("User not found.");

        if (currentUserId == userId && !dto.ProjectPermissions.Contains(ProjectPermission.ManageUsers))
            throw new BadRequestException("You cannot remove ManageUsers permission from yourself.");

        var currentProjectPermissions = await _projectUserRepository.GetUserProjectPermissionsAsync(userId, projectId);
        var currentScopePermissions = await _projectUserRepository.GetUserScopePermissionsAsync(userId, projectId);

        var oldValue = new { ProjectPermissions = currentProjectPermissions, ScopePermissions = currentScopePermissions };

        var validScopePermissions = await ValidateScopePermissionsAsync(dto.ScopePermissions, projectId);

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            await _projectUserRepository.RemoveAllProjectPermissionsAsync(projectUser.Id);
            await _projectUserRepository.RemoveAllScopePermissionsAsync(projectUser.Id);
            await _projectUserRepository.AddProjectPermissionsAsync(projectUser.Id, dto.ProjectPermissions);

            if (validScopePermissions.Count > 0)
                await _projectUserRepository.AddScopePermissionsAsync(projectUser.Id, validScopePermissions);

            await _unitOfWork.CommitAsync();
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }

        var newValue = new { ProjectPermissions = dto.ProjectPermissions, ScopePermissions = dto.ScopePermissions };

        _auditLogger.LogProjectAudit(project.Alias, actorUsername, "ProjectMember", null, "PermissionsUpdated", oldValue, newValue);

        return await MapToResponseDtoAsync(projectUser, user);
    }

    #region Helper Methods

    private async Task<IReadOnlyDictionary<Guid, IEnumerable<ScopePermission>>> ValidateScopePermissionsAsync(
        Dictionary<Guid, List<ScopePermission>> scopePermissions, Guid projectId)
    {
        var result = new Dictionary<Guid, IEnumerable<ScopePermission>>();
        foreach (var (scopeId, permissions) in scopePermissions)
        {
            var scope = await _scopeRepository.GetByIdAsync(scopeId);
            if (scope == null)
                throw new NotFoundException($"Scope {scopeId} not found.");

            if (scope.ProjectId != projectId)
                throw new BadRequestException($"Scope {scopeId} does not belong to this project.");

            result[scopeId] = permissions;
        }

        return result;
    }

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
