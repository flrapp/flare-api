using Flare.Application.Audit;
using Flare.Application.DTOs;
using Flare.Application.Interfaces;
using Flare.Domain.Entities;
using Flare.Domain.Enums;
using Flare.Infrastructure.Data.Repositories.Interfaces;

namespace Flare.Application.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IProjectUserRepository _projectUserRepository;
    private readonly IAuthService _authService;
    private readonly IAuditLogger _auditLogger;

    public UserService(
        IUserRepository userRepository,
        IProjectUserRepository projectUserRepository,
        IAuthService authService,
        IAuditLogger auditLogger)
    {
        _userRepository = userRepository;
        _projectUserRepository = projectUserRepository;
        _authService = authService;
        _auditLogger = auditLogger;
    }

    public async Task<UserResponseDto> CreateUserAsync(CreateUserDto dto, Guid createdByUserId, string actorUsername)
    {
        var exists = await _userRepository.ExistsByUsernameAsync(dto.Username);

        if (exists)
        {
            throw new InvalidOperationException("User with this username already exists");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = dto.Username,
            FullName = dto.FullName,
            PasswordHash = _authService.HashPassword(dto.TemporaryPassword),
            GlobalRole = GlobalRole.User,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            MustChangePassword = true
        };

        var createdUser = await _userRepository.AddAsync(user);

        _auditLogger.LogUserAudit(createdUser.Username, actorUsername, "User", null, "Created");

        return new UserResponseDto
        {
            UserId = createdUser.Id,
            Username = createdUser.Username,
            FullName = createdUser.FullName,
            GlobalRole = createdUser.GlobalRole,
            CreatedAt = createdUser.CreatedAt,
            LastLoginAt = createdUser.LastLoginAt
        };
    }

    public async Task<List<UserResponseDto>> GetAllUsersAsync()
    {
        var users = await _userRepository.GetAllAsync();

        return users.Select(u => new UserResponseDto
        {
            UserId = u.Id,
            Username = u.Username,
            FullName = u.FullName,
            GlobalRole = u.GlobalRole,
            CreatedAt = u.CreatedAt,
            LastLoginAt = u.LastLoginAt
        }).ToList();
    }

    public async Task<UserResponseDto> GetUserByIdAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);

        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        return new UserResponseDto
        {
            UserId = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            GlobalRole = user.GlobalRole,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        };
    }

    public async Task<UserResponseDto> UpdateUserAsync(Guid userId, UpdateUserDto dto, string actorUsername)
    {
        var user = await _userRepository.GetByIdAsync(userId);

        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        var oldValue = new { user.FullName, user.GlobalRole };

        user.FullName = dto.FullName;
        user.GlobalRole = dto.GlobalRole;

        await _userRepository.UpdateAsync(user);

        var newValue = new { dto.FullName, dto.GlobalRole };
        _auditLogger.LogUserAudit(user.Username, actorUsername, "User", null, "Updated", oldValue, newValue);

        return new UserResponseDto
        {
            UserId = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            GlobalRole = user.GlobalRole,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        };
    }

    public async Task SoftDeleteUserAsync(Guid userId, string actorUsername)
    {
        var user = await _userRepository.GetByIdAsync(userId);

        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        user.IsActive = false;
        await _userRepository.UpdateAsync(user);

        _auditLogger.LogUserAudit(user.Username, actorUsername, "User", null, "Deactivated");
    }

    public async Task<List<AvailableUserDto>> GetAvailableUsersForProjectAsync(Guid projectId)
    {
        var allUsers = await _userRepository.GetAllActiveUsersAsync();
        var projectUsers = await _projectUserRepository.GetByProjectIdAsync(projectId);
        var projectUserIds = projectUsers.Select(pu => pu.UserId).ToHashSet();

        return allUsers.Select(u => new AvailableUserDto
        {
            UserId = u.Id,
            Username = u.Username,
            FullName = u.FullName,
            IsAlreadyMember = projectUserIds.Contains(u.Id)
        }).ToList();
    }
}
