using Domian.Entities;
using Domian.Enums;
using Flare.Application.DTOs;
using Flare.Application.Interfaces;
using Flare.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Flare.Application.Services;

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _context;

    public AuthService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AuthResultDto?> LoginAsync(LoginDto loginDto)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == loginDto.Email);

        if (user == null || !user.IsActive)
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
            Email = user.Email,
            FullName = user.FullName,
            GlobalRole = user.GlobalRole
        };
    }

    public async Task<AuthResultDto> RegisterAsync(RegisterDto registerDto)
    {
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == registerDto.Email);

        if (existingUser != null)
        {
            throw new InvalidOperationException("User with this email already exists");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = registerDto.Email,
            PasswordHash = HashPassword(registerDto.Password),
            FullName = registerDto.FullName,
            GlobalRole = GlobalRole.User,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return new AuthResultDto
        {
            UserId = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            GlobalRole = user.GlobalRole
        };
    }

    public async Task<User?> GetUserByIdAsync(Guid userId)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email && u.IsActive);
    }

    public async Task UpdateLastLoginAsync(Guid userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user != null)
        {
            user.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
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
