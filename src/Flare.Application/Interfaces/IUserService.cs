using Flare.Application.DTOs;

namespace Flare.Application.Interfaces;

public interface IUserService
{
    Task<UserResponseDto> CreateUserAsync(CreateUserDto dto, Guid createdByUserId);
    Task<List<UserResponseDto>> GetAllUsersAsync();
    Task<UserResponseDto> GetUserByIdAsync(Guid userId);
    Task<UserResponseDto> UpdateUserAsync(Guid userId, UpdateUserDto dto);
    Task SoftDeleteUserAsync(Guid userId);
    Task<List<AvailableUserDto>> GetAvailableUsersForProjectAsync(Guid projectId);
}
