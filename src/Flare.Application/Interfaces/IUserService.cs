using Flare.Application.DTOs;

namespace Flare.Application.Interfaces;

public interface IUserService
{
    Task<UserResponseDto> CreateUserAsync(CreateUserDto dto, Guid createdByUserId, string actorUsername);
    Task<List<UserResponseDto>> GetAllUsersAsync();
    Task<UserResponseDto> GetUserByIdAsync(Guid userId);
    Task<UserResponseDto> UpdateUserAsync(Guid userId, UpdateUserDto dto, string actorUsername);
    Task SoftDeleteUserAsync(Guid userId, string actorUsername);
    Task<List<AvailableUserDto>> GetAvailableUsersForProjectAsync(Guid projectId);
}
