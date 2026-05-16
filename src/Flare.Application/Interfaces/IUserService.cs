using Flare.Application.DTOs;

namespace Flare.Application.Interfaces;

public interface IUserService
{
    Task<UserResponseDto> CreateUserAsync(CreateUserDto dto, Guid createdByUserId, string actorUsername);
    Task<PagedResult<UserResponseDto>> GetAllUsersAsync(bool? isActive = null, string? search = null, int page = 1, int pageSize = 20);
    Task<UserResponseDto> UpdateUserAsync(Guid userId, UpdateUserDto dto, string actorUsername);
    Task<List<AvailableUserDto>> GetAvailableUsersForProjectAsync(Guid projectId, string? search = null);
    Task ResetUserPasswordAsync(Guid userId, ResetUserPasswordDto dto, string actorUsername);
    Task ActivateUserAsync(Guid userId, string actorUsername);
    Task DeactivateUserAsync(Guid userId, Guid currentUserId, string actorUsername);
    Task HardDeleteUserAsync(Guid userId, Guid currentUserId, string actorUsername);
    Task UnlockUserAsync(Guid userId, string actorUsername);
}
