using Flare.Application.Services;
using Flare.Domain.Entities;
using Flare.Domain.Enums;
using Flare.Infrastructure.Data.Repositories.Interfaces;
using NSubstitute;

namespace Flare.UnitTests.Application;

public class PermissionServiceTests
{
    private readonly IProjectUserRepository _projectUsers = Substitute.For<IProjectUserRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IScopeRepository _scopes = Substitute.For<IScopeRepository>();
    private readonly PermissionService _sut;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();

    public PermissionServiceTests()
    {
        _sut = new PermissionService(_projectUsers, _users, _scopes);
    }

    private void GivenRole(GlobalRole role) =>
        _users.GetByIdAsync(_userId).Returns(new User { Id = _userId, GlobalRole = role });

    [Theory]
    [InlineData(GlobalRole.Admin, true)]
    [InlineData(GlobalRole.SuperAdmin, true)]
    [InlineData(GlobalRole.User, false)]
    public async Task IsAdmin_depends_on_global_role(GlobalRole role, bool expected)
    {
        GivenRole(role);

        Assert.Equal(expected, await _sut.IsAdminAsync(_userId));
    }

    [Fact]
    public async Task IsAdmin_unknown_user_is_false()
    {
        Assert.False(await _sut.IsAdminAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Admin_bypasses_membership_and_permission_checks()
    {
        GivenRole(GlobalRole.Admin);
        var scopeId = Guid.NewGuid();

        Assert.True(await _sut.IsProjectMemberAsync(_userId, _projectId));
        Assert.True(await _sut.HasProjectPermissionAsync(_userId, _projectId, ProjectPermission.DeleteProject));
        Assert.True(await _sut.HasScopePermissionAsync(_userId, scopeId, ScopePermission.UpdateFeatureFlags));

        await _projectUsers.DidNotReceiveWithAnyArgs().ExistsAsync(default, default);
        await _projectUsers.DidNotReceiveWithAnyArgs().HasProjectPermissionAsync(default, default, default);
        await _projectUsers.DidNotReceiveWithAnyArgs().HasScopePermissionAsync(default, default, default);
    }

    [Fact]
    public async Task Regular_user_checks_are_delegated_to_repository()
    {
        GivenRole(GlobalRole.User);
        var scopeId = Guid.NewGuid();
        _projectUsers.ExistsAsync(_userId, _projectId).Returns(true);
        _projectUsers.HasProjectPermissionAsync(_userId, _projectId, ProjectPermission.ManageUsers).Returns(false);
        _projectUsers.HasScopePermissionAsync(_userId, scopeId, ScopePermission.ReadFeatureFlags).Returns(true);

        Assert.True(await _sut.IsProjectMemberAsync(_userId, _projectId));
        Assert.False(await _sut.HasProjectPermissionAsync(_userId, _projectId, ProjectPermission.ManageUsers));
        Assert.True(await _sut.HasScopePermissionAsync(_userId, scopeId, ScopePermission.ReadFeatureFlags));
    }

    [Fact]
    public async Task Admin_gets_every_project_permission()
    {
        GivenRole(GlobalRole.SuperAdmin);

        var permissions = await _sut.GetUserProjectPermissionsAsync(_userId, _projectId);

        Assert.Equal(Enum.GetValues<ProjectPermission>(), permissions);
    }

    [Fact]
    public async Task Regular_user_project_permissions_come_from_repository()
    {
        GivenRole(GlobalRole.User);
        _projectUsers.GetUserProjectPermissionsAsync(_userId, _projectId).Returns([ProjectPermission.ViewApiKey]);

        var permissions = await _sut.GetUserProjectPermissionsAsync(_userId, _projectId);

        Assert.Equal([ProjectPermission.ViewApiKey], permissions);
    }

    [Fact]
    public async Task Admin_gets_all_scope_permissions_for_every_project_scope()
    {
        GivenRole(GlobalRole.Admin);
        var scopes = new List<Scope> { new() { Id = Guid.NewGuid() }, new() { Id = Guid.NewGuid() } };
        _scopes.GetByProjectIdAsync(_projectId).Returns(scopes);

        var result = await _sut.GetUserScopePermissionsAsync(_userId, _projectId);

        Assert.Equal(scopes.Select(s => s.Id), result.Keys);
        Assert.All(result.Values, p => Assert.Equal(Enum.GetValues<ScopePermission>(), p));
    }

    [Fact]
    public async Task Regular_user_scope_permissions_come_from_repository()
    {
        GivenRole(GlobalRole.User);
        var expected = new Dictionary<Guid, List<ScopePermission>> { [Guid.NewGuid()] = [ScopePermission.ReadFeatureFlags] };
        _projectUsers.GetUserScopePermissionsAsync(_userId, _projectId).Returns(expected);

        Assert.Same(expected, await _sut.GetUserScopePermissionsAsync(_userId, _projectId));
    }
}
