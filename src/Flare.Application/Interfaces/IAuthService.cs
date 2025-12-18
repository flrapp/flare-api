using Domian.Entities;
using Flare.Application.DTOs;

namespace Flare.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResultDto?> LoginAsync(LoginDto loginDto);
    Task<AuthResultDto> RegisterAsync(RegisterDto registerDto);
    Task<User?> GetUserByIdAsync(Guid userId);
    Task<User?> GetUserByEmailAsync(string email);
    Task UpdateLastLoginAsync(Guid userId);
    string HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash);
}
