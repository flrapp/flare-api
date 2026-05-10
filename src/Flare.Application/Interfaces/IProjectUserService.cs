using Flare.Application.DTOs;

namespace Flare.Application.Interfaces;

public interface IProjectUserService
{
    Task<ProjectUserResponseDto> InviteUserAsync(Guid projectId, InviteUserDto dto, Guid inviterUserId, string actorUsername);
    Task RemoveUserAsync(Guid projectId, Guid userId, Guid currentUserId, string actorUsername);
    Task<List<ProjectUserResponseDto>> GetProjectUsersAsync(Guid projectId, Guid currentUserId);
    Task<ProjectUserResponseDto> UpdateUserPermissionsAsync(Guid projectId, Guid userId, UpdateUserPermissionsDto dto, Guid currentUserId, string actorUsername);
}
