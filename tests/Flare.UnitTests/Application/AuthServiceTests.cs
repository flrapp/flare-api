using Flare.Application.DTOs;
using Flare.Application.Services;
using Flare.Domain.Constants;
using Flare.Domain.Entities;
using Flare.Domain.Enums;
using Flare.Domain.Exceptions;
using Flare.Infrastructure.Data.Repositories.Interfaces;
using NSubstitute;

namespace Flare.UnitTests.Application;

public class AuthServiceTests
{
    private const string Password = "Secret123";

    // Low work factor keeps the suite fast; verification works regardless of cost.
    private static readonly string PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password, workFactor: 4);

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _sut = new AuthService(_users);
    }

    private User GivenUser(Action<User>? configure = null)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "alice",
            FullName = "Alice A",
            PasswordHash = PasswordHash,
            GlobalRole = GlobalRole.User,
            IsActive = true,
            MustChangePassword = true
        };
        configure?.Invoke(user);
        _users.GetByUsernameAsync(user.Username).Returns(user);
        _users.GetByIdAsync(user.Id).Returns(user);
        return user;
    }

    private Task<AuthResultDto?> Login(string password = Password) =>
        _sut.LoginAsync(new LoginDto { Username = "alice", Password = password });

    [Fact]
    public async Task Login_unknown_user_returns_null()
    {
        Assert.Null(await Login());
        await _users.DidNotReceive().UpdateAsync(Arg.Any<User>());
    }

    [Fact]
    public async Task Login_valid_credentials_returns_result_and_resets_counters()
    {
        var user = GivenUser(u =>
        {
            u.FailedLoginAttempts = 2;
            u.LastFailedLoginAt = DateTime.UtcNow;
        });

        var result = await Login();

        Assert.NotNull(result);
        Assert.Equal(user.Id, result.UserId);
        Assert.Equal("alice", result.Username);
        Assert.Equal("Alice A", result.FullName);
        Assert.True(result.MustChangePassword);
        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.Null(user.LastFailedLoginAt);
        await _users.Received(1).UpdateAsync(user);
    }

    [Fact]
    public async Task Login_permanently_locked_user_throws()
    {
        GivenUser(u => u.IsBruteForceLocked = true);

        var ex = await Assert.ThrowsAsync<AccountLockedException>(() => Login());

        Assert.True(ex.IsPermanent);
    }

    [Fact]
    public async Task Login_temporarily_locked_user_throws_with_remaining_minutes()
    {
        GivenUser(u => u.LockedUntil = DateTime.UtcNow.AddMinutes(10));

        var ex = await Assert.ThrowsAsync<AccountLockedException>(() => Login());

        Assert.False(ex.IsPermanent);
        Assert.InRange(ex.RemainingMinutes!.Value, 9, 10);
    }

    [Fact]
    public async Task Login_expired_lock_allows_login()
    {
        GivenUser(u => u.LockedUntil = DateTime.UtcNow.AddMinutes(-1));

        Assert.NotNull(await Login());
    }

    [Fact]
    public async Task Login_inactive_user_returns_null()
    {
        GivenUser(u => u.IsActive = false);

        Assert.Null(await Login());
    }

    [Fact]
    public async Task Login_wrong_password_increments_attempts_and_returns_null()
    {
        var user = GivenUser();

        var result = await Login("WrongPass1");

        Assert.Null(result);
        Assert.Equal(1, user.FailedLoginAttempts);
        Assert.NotNull(user.LastFailedLoginAt);
        await _users.Received(1).UpdateAsync(user);
    }

    [Fact]
    public async Task Login_wrong_password_after_reset_window_restarts_counter()
    {
        var user = GivenUser(u =>
        {
            u.FailedLoginAttempts = 2;
            u.LastFailedLoginAt = DateTime.UtcNow - AuthConstants.AttemptResetWindow - TimeSpan.FromMinutes(1);
        });

        await Login("WrongPass1");

        Assert.Equal(1, user.FailedLoginAttempts);
    }

    [Fact]
    public async Task Login_reaching_max_attempts_locks_temporarily()
    {
        var user = GivenUser(u =>
        {
            u.FailedLoginAttempts = AuthConstants.MaxFailedAttempts - 1;
            u.LastFailedLoginAt = DateTime.UtcNow;
        });

        var ex = await Assert.ThrowsAsync<AccountLockedException>(() => Login("WrongPass1"));

        Assert.False(ex.IsPermanent);
        Assert.Equal((int)AuthConstants.TemporaryLockDuration.TotalMinutes, ex.RemainingMinutes);
        Assert.NotNull(user.LockedUntil);
        Assert.False(user.IsBruteForceLocked);
    }

    [Fact]
    public async Task Login_reaching_permanent_threshold_locks_permanently()
    {
        var user = GivenUser(u =>
        {
            u.FailedLoginAttempts = AuthConstants.PermanentLockThreshold - 1;
            u.LastFailedLoginAt = DateTime.UtcNow;
        });

        var ex = await Assert.ThrowsAsync<AccountLockedException>(() => Login("WrongPass1"));

        Assert.True(ex.IsPermanent);
        Assert.True(user.IsBruteForceLocked);
        await _users.Received(1).UpdateAsync(user);
    }

    [Fact]
    public async Task GetUserById_and_GetUserByUsername_delegate_to_active_lookups()
    {
        var user = new User { Id = Guid.NewGuid(), Username = "bob" };
        _users.GetActiveByIdAsync(user.Id).Returns(user);
        _users.GetActiveByUsernameAsync("bob").Returns(user);

        Assert.Same(user, await _sut.GetUserByIdAsync(user.Id));
        Assert.Same(user, await _sut.GetUserByUsernameAsync("bob"));
    }

    [Fact]
    public async Task UpdateLastLogin_sets_timestamp_when_user_exists()
    {
        var user = GivenUser();

        await _sut.UpdateLastLoginAsync(user.Id);

        Assert.NotNull(user.LastLoginAt);
        await _users.Received(1).UpdateAsync(user);
    }

    [Fact]
    public async Task UpdateLastLogin_does_nothing_for_unknown_user()
    {
        await _sut.UpdateLastLoginAsync(Guid.NewGuid());

        await _users.DidNotReceive().UpdateAsync(Arg.Any<User>());
    }

    [Fact]
    public async Task ChangePassword_updates_hash_and_clears_flag()
    {
        var user = GivenUser();

        await _sut.ChangePasswordAsync(user.Id, new ChangePasswordDto { CurrentPassword = Password, NewPassword = "NewSecret1" });

        Assert.True(BCrypt.Net.BCrypt.Verify("NewSecret1", user.PasswordHash));
        Assert.False(user.MustChangePassword);
        await _users.Received(1).UpdateAsync(user);
    }

    [Fact]
    public async Task ChangePassword_unknown_user_throws()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.ChangePasswordAsync(Guid.NewGuid(), new ChangePasswordDto { CurrentPassword = "x", NewPassword = "y" }));

        Assert.Equal("User not found", ex.Message);
    }

    [Fact]
    public async Task ChangePassword_wrong_current_password_throws()
    {
        var user = GivenUser();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.ChangePasswordAsync(user.Id, new ChangePasswordDto { CurrentPassword = "Nope1234", NewPassword = "NewSecret1" }));

        Assert.Equal("Current password is incorrect", ex.Message);
        await _users.DidNotReceive().UpdateAsync(Arg.Any<User>());
    }

    [Fact]
    public async Task UnlockAccount_clears_all_lock_state()
    {
        var user = GivenUser(u =>
        {
            u.IsBruteForceLocked = true;
            u.FailedLoginAttempts = 5;
            u.LastFailedLoginAt = DateTime.UtcNow;
            u.LockedUntil = DateTime.UtcNow.AddMinutes(5);
        });

        await _sut.UnlockAccountAsync(user.Id);

        Assert.False(user.IsBruteForceLocked);
        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.Null(user.LastFailedLoginAt);
        Assert.Null(user.LockedUntil);
    }

    [Fact]
    public async Task UnlockAccount_unknown_user_throws_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.UnlockAccountAsync(Guid.NewGuid()));
    }

    [Fact]
    public void HashPassword_produces_verifiable_bcrypt_hash()
    {
        var hash = _sut.HashPassword("Pa55word");

        Assert.StartsWith("$2", hash);
        Assert.True(BCrypt.Net.BCrypt.Verify("Pa55word", hash));
    }
}
