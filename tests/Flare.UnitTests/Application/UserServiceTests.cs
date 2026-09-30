using Flare.Application.Audit;
using Flare.Application.DTOs;
using Flare.Application.Interfaces;
using Flare.Application.Services;
using Flare.Domain.Entities;
using Flare.Domain.Enums;
using Flare.Domain.Exceptions;
using Flare.Infrastructure.Data.Repositories.Interfaces;
using NSubstitute;

namespace Flare.UnitTests.Application;

public class UserServiceTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IProjectUserRepository _projectUsers = Substitute.For<IProjectUserRepository>();
    private readonly IAuthService _auth = Substitute.For<IAuthService>();
    private readonly IAuditLogger _audit = Substitute.For<IAuditLogger>();
    private readonly UserService _sut;

    public UserServiceTests()
    {
        _sut = new UserService(_users, _projectUsers, _auth, _audit);
        _auth.HashPassword(Arg.Any<string>()).Returns(ci => "hashed:" + ci.Arg<string>());
        _users.AddAsync(Arg.Any<User>()).Returns(ci => ci.Arg<User>());
    }

    private User GivenUser(string username = "bob", bool active = true, string fullName = "Bob B")
    {
        var user = new User { Id = Guid.NewGuid(), Username = username, FullName = fullName, IsActive = active };
        _users.GetByIdAsync(user.Id).Returns(user);
        return user;
    }

    [Fact]
    public async Task CreateUser_hashes_password_and_forces_change()
    {
        var dto = new CreateUserDto { Username = "carol", FullName = "Carol C", TemporaryPassword = "TempPass1" };

        var result = await _sut.CreateUserAsync(dto, Guid.NewGuid(), "admin");

        Assert.Equal("carol", result.Username);
        Assert.Equal(GlobalRole.User, result.GlobalRole);
        Assert.True(result.IsActive);
        await _users.Received(1).AddAsync(Arg.Is<User>(u =>
            u.PasswordHash == "hashed:TempPass1" && u.MustChangePassword && u.Username == "carol"));
        _audit.Received(1).LogUserAudit("carol", "admin", "User", null, "Created");
    }

    [Fact]
    public async Task CreateUser_duplicate_username_throws()
    {
        _users.ExistsByUsernameAsync("carol").Returns(true);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.CreateUserAsync(new CreateUserDto { Username = "carol", FullName = "C", TemporaryPassword = "x" }, Guid.NewGuid(), "admin"));

        await _users.DidNotReceive().AddAsync(Arg.Any<User>());
    }

    [Fact]
    public async Task GetAllUsers_filters_by_active_and_search_and_paginates()
    {
        _users.GetAllAsync().Returns(
        [
            new User { Username = "alpha", FullName = "First", IsActive = true },
            new User { Username = "beta", FullName = "Alpha Beta", IsActive = true },
            new User { Username = "gamma", FullName = "Gamma", IsActive = true },
            new User { Username = "alphaz", FullName = "Inactive", IsActive = false }
        ]);

        var result = await _sut.GetAllUsersAsync(isActive: true, search: "  ALPHA ", page: 1, pageSize: 1);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal("alpha", Assert.Single(result.Items).Username);
    }

    [Fact]
    public async Task GetAllUsers_without_filters_returns_everyone()
    {
        _users.GetAllAsync().Returns([new User { Username = "a" }, new User { Username = "b", IsActive = false }]);

        var result = await _sut.GetAllUsersAsync();

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
    }

    [Fact]
    public async Task UpdateUser_changes_name_and_role_and_audits_old_and_new()
    {
        var user = GivenUser();

        var result = await _sut.UpdateUserAsync(user.Id, new UpdateUserDto { FullName = "Robert", GlobalRole = GlobalRole.Admin }, "admin");

        Assert.Equal("Robert", result.FullName);
        Assert.Equal(GlobalRole.Admin, result.GlobalRole);
        await _users.Received(1).UpdateAsync(user);
        _audit.Received(1).LogUserAudit("bob", "admin", "User", null, "Updated", Arg.Any<object>(), Arg.Any<object>());
    }

    [Fact]
    public async Task UpdateUser_unknown_user_throws()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.UpdateUserAsync(Guid.NewGuid(), new UpdateUserDto { FullName = "x" }, "admin"));
    }

    [Fact]
    public async Task ResetUserPassword_sets_new_hash_and_must_change()
    {
        var user = GivenUser();

        await _sut.ResetUserPasswordAsync(user.Id, new ResetUserPasswordDto { TemporaryPassword = "Temp1234" }, "admin");

        Assert.Equal("hashed:Temp1234", user.PasswordHash);
        Assert.True(user.MustChangePassword);
        _audit.Received(1).LogUserAudit("bob", "admin", "User", null, "PasswordReset");
    }

    [Fact]
    public async Task Activate_and_deactivate_toggle_flag()
    {
        var user = GivenUser(active: false);

        await _sut.ActivateUserAsync(user.Id, "admin");
        Assert.True(user.IsActive);

        await _sut.DeactivateUserAsync(user.Id, Guid.NewGuid(), "admin");
        Assert.False(user.IsActive);

        _audit.Received(1).LogUserAudit("bob", "admin", "User", null, "Activated");
        _audit.Received(1).LogUserAudit("bob", "admin", "User", null, "Deactivated");
    }

    [Fact]
    public async Task Deactivate_self_is_rejected()
    {
        var id = Guid.NewGuid();

        await Assert.ThrowsAsync<BadRequestException>(() => _sut.DeactivateUserAsync(id, id, "admin"));
    }

    [Fact]
    public async Task HardDelete_removes_user()
    {
        var user = GivenUser();

        await _sut.HardDeleteUserAsync(user.Id, Guid.NewGuid(), "admin");

        await _users.Received(1).DeleteAsync(user);
        _audit.Received(1).LogUserAudit("bob", "admin", "User", null, "HardDeleted");
    }

    [Fact]
    public async Task HardDelete_self_is_rejected()
    {
        var id = Guid.NewGuid();

        await Assert.ThrowsAsync<BadRequestException>(() => _sut.HardDeleteUserAsync(id, id, "admin"));
    }

    [Fact]
    public async Task UnlockUser_delegates_to_auth_service()
    {
        var user = GivenUser();

        await _sut.UnlockUserAsync(user.Id, "admin");

        await _auth.Received(1).UnlockAccountAsync(user.Id);
        _audit.Received(1).LogUserAudit("bob", "admin", "User", null, "BruteForceUnlocked");
    }

    public static TheoryData<string> UnknownUserOperations => new() { "reset", "activate", "deactivate", "delete", "unlock" };

    [Theory]
    [MemberData(nameof(UnknownUserOperations))]
    public async Task Operations_on_unknown_user_throw_not_found(string operation)
    {
        var id = Guid.NewGuid();
        Func<Task> act = operation switch
        {
            "reset" => () => _sut.ResetUserPasswordAsync(id, new ResetUserPasswordDto(), "admin"),
            "activate" => () => _sut.ActivateUserAsync(id, "admin"),
            "deactivate" => () => _sut.DeactivateUserAsync(id, Guid.NewGuid(), "admin"),
            "delete" => () => _sut.HardDeleteUserAsync(id, Guid.NewGuid(), "admin"),
            _ => () => _sut.UnlockUserAsync(id, "admin")
        };

        await Assert.ThrowsAsync<NotFoundException>(act);
    }

    [Fact]
    public async Task GetAvailableUsers_marks_existing_members_and_limits_to_ten()
    {
        var projectId = Guid.NewGuid();
        var member = new User { Id = Guid.NewGuid(), Username = "member", FullName = "M" };
        var others = Enumerable.Range(0, 15).Select(i => new User { Id = Guid.NewGuid(), Username = $"user{i}", FullName = "U" });
        _users.GetAllActiveUsersAsync().Returns([member, .. others]);
        _projectUsers.GetByProjectIdAsync(projectId).Returns([new ProjectUser { UserId = member.Id }]);

        var result = await _sut.GetAvailableUsersForProjectAsync(projectId);

        Assert.Equal(10, result.Count);
        Assert.True(result.Single(u => u.Username == "member").IsAlreadyMember);
        Assert.All(result.Where(u => u.Username != "member"), u => Assert.False(u.IsAlreadyMember));
    }

    [Fact]
    public async Task GetAvailableUsers_filters_by_search_term()
    {
        var projectId = Guid.NewGuid();
        _users.GetAllActiveUsersAsync().Returns(
        [
            new User { Id = Guid.NewGuid(), Username = "dave", FullName = "Dave D" },
            new User { Id = Guid.NewGuid(), Username = "erin", FullName = "Erin Davidson" },
            new User { Id = Guid.NewGuid(), Username = "frank", FullName = "Frank" }
        ]);
        _projectUsers.GetByProjectIdAsync(projectId).Returns([]);

        var result = await _sut.GetAvailableUsersForProjectAsync(projectId, " dav ");

        Assert.Equal(["dave", "erin"], result.Select(u => u.Username));
    }
}
