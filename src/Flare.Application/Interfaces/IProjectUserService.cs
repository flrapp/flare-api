using Flare.Application.DTOs;

namespace Flare.Application.Interfaces;

public interface IProjectUserService
{
    Task<ProjectUserResponseDto> InviteUserAsync(Guid projectId, InviteUserDto dto, Guid inviterUserId, string actorUsername);
    Task RemoveUserAsync(Guid projectId, Guid userId, Guid currentUserId, string actorUsername);
    Task<PagedResult<ProjectUserResponseDto>> GetProjectUsersAsync(Guid projectId, Guid currentUserId, string? search = null, int page = 1, int pageSize = 20);
    Task<ProjectUserResponseDto> UpdateUserPermissionsAsync(Guid projectId, Guid userId, UpdateUserPermissionsDto dto, Guid currentUserId, string actorUsername);
}
