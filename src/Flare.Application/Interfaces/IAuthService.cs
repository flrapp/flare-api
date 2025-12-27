using Flare.Application.DTOs;
using Flare.Domain.Entities;

namespace Flare.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResultDto?> LoginAsync(LoginDto loginDto);
    Task<AuthResultDto> RegisterAsync(RegisterDto registerDto);
    Task<User?> GetUserByIdAsync(Guid userId);
    Task<User?> GetUserByUsernameAsync(string username);
    Task UpdateLastLoginAsync(Guid userId);
    string HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash);
}
