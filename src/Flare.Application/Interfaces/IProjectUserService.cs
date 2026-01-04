using Flare.Application.DTOs;
using Flare.Domain.Enums;

namespace Flare.Application.Interfaces;

public interface IProjectUserService
{
    Task<ProjectUserResponseDto> InviteUserAsync(Guid projectId, InviteUserDto dto, Guid inviterUserId);
    Task RemoveUserAsync(Guid projectId, Guid userId, Guid currentUserId);
    Task<List<ProjectUserResponseDto>> GetProjectUsersAsync(Guid projectId, Guid currentUserId);
    Task<ProjectUserResponseDto> GetProjectUserAsync(Guid projectId, Guid userId, Guid currentUserId);
    Task AssignProjectPermissionsAsync(Guid projectId, Guid userId, AssignProjectPermissionsDto dto, Guid currentUserId);
    Task RevokeProjectPermissionAsync(Guid projectId, Guid userId, ProjectPermission permission, Guid currentUserId);
    Task AssignScopePermissionsAsync(Guid projectId, Guid userId, AssignScopePermissionsDto dto, Guid currentUserId);
    Task RevokeScopePermissionAsync(Guid projectId, Guid userId, Guid scopeId, ScopePermission permission, Guid currentUserId);
    Task<ProjectUserResponseDto> UpdateUserPermissionsAsync(Guid projectId, Guid userId, UpdateUserPermissionsDto dto, Guid currentUserId);
}
