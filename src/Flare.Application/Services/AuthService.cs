using Flare.Application.DTOs;
using Flare.Application.Interfaces;
using Flare.Domain.Entities;
using Flare.Domain.Enums;
using Flare.Infrastructure.Data.Repositories.Interfaces;

namespace Flare.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;

    public AuthService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<AuthResultDto?> LoginAsync(LoginDto loginDto)
    {
        var user = await _userRepository.GetActiveByUsernameAsync(loginDto.Username);

        if (user == null)
        {
            return null;
        }

        if (!VerifyPassword(loginDto.Password, user.PasswordHash))
        {
            return null;
        }

        return new AuthResultDto
        {
            UserId = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            GlobalRole = user.GlobalRole,
            MustChangePassword = user.MustChangePassword
        };
    }

    public async Task<AuthResultDto> RegisterAsync(RegisterDto registerDto)
    {
        var exists = await _userRepository.ExistsByUsernameAsync(registerDto.Username);

        if (exists)
        {
            throw new InvalidOperationException("User with this username already exists");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = registerDto.Username,
            PasswordHash = HashPassword(registerDto.Password),
            FullName = registerDto.FullName,
            GlobalRole = GlobalRole.User,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _userRepository.AddAsync(user);

        return new AuthResultDto
        {
            UserId = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            GlobalRole = user.GlobalRole,
            MustChangePassword = user.MustChangePassword
        };
    }

    public async Task<User?> GetUserByIdAsync(Guid userId)
    {
        return await _userRepository.GetActiveByIdAsync(userId);
    }

    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        return await _userRepository.GetActiveByUsernameAsync(username);
    }

    public async Task UpdateLastLoginAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user != null)
        {
            user.LastLoginAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user);
        }
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordDto dto)
    {
        var user = await _userRepository.GetByIdAsync(userId);

        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        if (!VerifyPassword(dto.CurrentPassword, user.PasswordHash))
        {
            throw new InvalidOperationException("Current password is incorrect");
        }

        user.PasswordHash = HashPassword(dto.NewPassword);
        user.MustChangePassword = false;

        await _userRepository.UpdateAsync(user);
    }

    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        return BCrypt.Net.BCrypt.Verify(password, passwordHash);
    }
}
