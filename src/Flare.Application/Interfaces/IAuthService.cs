using Flare.Application.DTOs;
using Flare.Domain.Entities;

namespace Flare.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResultDto?> LoginAsync(LoginDto loginDto);
    Task<User?> GetUserByIdAsync(Guid userId);
    Task<User?> GetUserByUsernameAsync(string username);
    Task UpdateLastLoginAsync(Guid userId);
    Task ChangePasswordAsync(Guid userId, ChangePasswordDto dto);
    Task UnlockAccountAsync(Guid userId);
    string HashPassword(string password);
}
