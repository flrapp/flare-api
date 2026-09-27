using Flare.Application.Audit;
using Flare.Application.DTOs;
using Flare.Application.Interfaces;
using Flare.Application.Services;
using Flare.Domain.Entities;
using Flare.Domain.Enums;
using Flare.Domain.Exceptions;
using Flare.Infrastructure.Data.Repositories.Interfaces;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Flare.UnitTests.Application;

public class ProjectUserServiceTests
{
    private readonly IProjectUserRepository _projectUsers = Substitute.For<IProjectUserRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IProjectRepository _projects = Substitute.For<IProjectRepository>();
    private readonly IScopeRepository _scopes = Substitute.For<IScopeRepository>();
    private readonly IPermissionService _permissions = Substitute.For<IPermissionService>();
    private readonly IAuditLogger _audit = Substitute.For<IAuditLogger>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ProjectUserService _sut;

    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Project _project = new() { Id = Guid.NewGuid(), Alias = "proj" };
    private readonly Scope _scope;

    public ProjectUserServiceTests()
    {
        _sut = new ProjectUserService(_projectUsers, _users, _projects, _scopes, _permissions, _audit, _uow);
        _projects.GetByIdAsync(_project.Id).Returns(_project);
        _scope = new Scope { Id = Guid.NewGuid(), ProjectId = _project.Id };
        _scopes.GetByIdAsync(_scope.Id).Returns(_scope);
        _projectUsers.GetUserProjectPermissionsAsync(default, default).ReturnsForAnyArgs([]);
        _projectUsers.GetUserScopePermissionsAsync(default, default).ReturnsForAnyArgs(new Dictionary<Guid, List<ScopePermission>>());
    }

    private void GrantManageUsers() =>
        _permissions.HasProjectPermissionAsync(_actorId, _project.Id, ProjectPermission.ManageUsers).Returns(true);

    private User GivenUser(bool active = true, string username = "bob", string fullName = "Bob B")
    {
        var user = new User { Id = Guid.NewGuid(), Username = username, FullName = fullName, IsActive = active };
        _users.GetByIdAsync(user.Id).Returns(user);
        return user;
    }

    #region Invite

    [Fact]
    public async Task Invite_adds_member_with_permissions_in_transaction()
    {
        GrantManageUsers();
        var user = GivenUser();
        var dto = new InviteUserDto
        {
            UserId = user.Id,
            ProjectPermissions = [ProjectPermission.ManageFeatureFlags],
            ScopePermissions = new() { [_scope.Id] = [ScopePermission.ReadFeatureFlags] }
        };

        var result = await _sut.InviteUserAsync(_project.Id, dto, _actorId, "alice");

        Assert.Equal(user.Id, result.UserId);
        Assert.Equal("bob", result.Username);
        Received.InOrder(() =>
        {
            _uow.BeginTransactionAsync();
            _projectUsers.AddAsync(Arg.Is<ProjectUser>(pu => pu.UserId == user.Id && pu.InvitedBy == _actorId));
            _projectUsers.AddProjectPermissionsAsync(Arg.Any<Guid>(), dto.ProjectPermissions);
            _projectUsers.AddScopePermissionsAsync(Arg.Any<Guid>(), Arg.Is<IReadOnlyDictionary<Guid, IEnumerable<ScopePermission>>>(d => d.ContainsKey(_scope.Id)));
            _uow.CommitAsync();
        });
        _audit.Received(1).LogProjectAudit("proj", "alice", "ProjectMember", null, "UserInvited");
    }

    [Fact]
    public async Task Invite_without_scope_permissions_skips_scope_insert()
    {
        GrantManageUsers();
        var user = GivenUser();

        await _sut.InviteUserAsync(_project.Id, new InviteUserDto { UserId = user.Id }, _actorId, "alice");

        await _projectUsers.DidNotReceiveWithAnyArgs().AddScopePermissionsAsync(default, default!);
    }

    [Fact]
    public async Task Invite_rolls_back_when_persistence_fails()
    {
        GrantManageUsers();
        var user = GivenUser();
        _projectUsers.AddProjectPermissionsAsync(Arg.Any<Guid>(), Arg.Any<IEnumerable<ProjectPermission>>())
            .ThrowsAsync(new InvalidOperationException("db down"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.InviteUserAsync(_project.Id, new InviteUserDto { UserId = user.Id }, _actorId, "alice"));

        await _uow.Received(1).RollbackAsync();
        await _uow.DidNotReceive().CommitAsync();
    }

    [Fact]
    public async Task Invite_validation_failures()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _sut.InviteUserAsync(_project.Id, new InviteUserDto { UserId = Guid.NewGuid() }, _actorId, "alice"));

        GrantManageUsers();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.InviteUserAsync(_project.Id, new InviteUserDto { UserId = Guid.NewGuid() }, _actorId, "alice"));

        var inactive = GivenUser(active: false);
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _sut.InviteUserAsync(_project.Id, new InviteUserDto { UserId = inactive.Id }, _actorId, "alice"));

        var member = GivenUser();
        _projectUsers.ExistsAsync(member.Id, _project.Id).Returns(true);
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _sut.InviteUserAsync(_project.Id, new InviteUserDto { UserId = member.Id }, _actorId, "alice"));
    }

    [Fact]
    public async Task Invite_to_missing_project_throws_not_found()
    {
        var missing = Guid.NewGuid();
        _permissions.HasProjectPermissionAsync(_actorId, missing, ProjectPermission.ManageUsers).Returns(true);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.InviteUserAsync(missing, new InviteUserDto { UserId = Guid.NewGuid() }, _actorId, "alice"));
    }

    [Fact]
    public async Task Invite_rejects_unknown_or_foreign_scopes()
    {
        GrantManageUsers();
        var user = GivenUser();

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.InviteUserAsync(_project.Id, new InviteUserDto
        {
            UserId = user.Id,
            ScopePermissions = new() { [Guid.NewGuid()] = [ScopePermission.ReadFeatureFlags] }
        }, _actorId, "alice"));

        var foreign = new Scope { Id = Guid.NewGuid(), ProjectId = Guid.NewGuid() };
        _scopes.GetByIdAsync(foreign.Id).Returns(foreign);
        await Assert.ThrowsAsync<BadRequestException>(() => _sut.InviteUserAsync(_project.Id, new InviteUserDto
        {
            UserId = user.Id,
            ScopePermissions = new() { [foreign.Id] = [ScopePermission.ReadFeatureFlags] }
        }, _actorId, "alice"));
    }

    #endregion

    #region Remove

    private ProjectUser GivenMember(User user, params ProjectPermission[] permissions)
    {
        var projectUser = new ProjectUser { Id = Guid.NewGuid(), ProjectId = _project.Id, UserId = user.Id };
        _projectUsers.GetByUserAndProjectAsync(user.Id, _project.Id).Returns(projectUser);
        foreach (var permission in permissions)
            _projectUsers.HasProjectPermissionAsync(user.Id, _project.Id, permission).Returns(true);
        return projectUser;
    }

    [Fact]
    public async Task Remove_deletes_membership_when_others_keep_admin_rights()
    {
        GrantManageUsers();
        var owner = GivenUser(username: "owner");
        var target = GivenUser(username: "target");
        var ownerMembership = GivenMember(owner, ProjectPermission.ManageUsers, ProjectPermission.DeleteProject);
        var targetMembership = GivenMember(target, ProjectPermission.ManageUsers, ProjectPermission.DeleteProject);
        _projectUsers.GetByProjectIdAsync(_project.Id).Returns([ownerMembership, targetMembership]);

        await _sut.RemoveUserAsync(_project.Id, target.Id, _actorId, "alice");

        await _projectUsers.Received(1).DeleteAsync(targetMembership.Id);
        _audit.Received(1).LogProjectAudit("proj", "alice", "ProjectMember", null, "UserRemoved");
    }

    [Fact]
    public async Task Remove_last_user_manager_is_rejected()
    {
        GrantManageUsers();
        var target = GivenUser();
        var membership = GivenMember(target, ProjectPermission.ManageUsers);
        _projectUsers.GetByProjectIdAsync(_project.Id).Returns([membership]);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => _sut.RemoveUserAsync(_project.Id, target.Id, _actorId, "alice"));

        Assert.Contains("ManageUsers", ex.Message);
    }

    [Fact]
    public async Task Remove_last_project_deleter_is_rejected()
    {
        GrantManageUsers();
        var target = GivenUser();
        var other = GivenUser(username: "other");
        var membership = GivenMember(target, ProjectPermission.DeleteProject);
        var otherMembership = GivenMember(other, ProjectPermission.ManageUsers);
        _projectUsers.GetByProjectIdAsync(_project.Id).Returns([membership, otherMembership]);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => _sut.RemoveUserAsync(_project.Id, target.Id, _actorId, "alice"));

        Assert.Contains("DeleteProject", ex.Message);
    }

    [Fact]
    public async Task Remove_validation_failures()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => _sut.RemoveUserAsync(_project.Id, Guid.NewGuid(), _actorId, "alice"));

        var missing = Guid.NewGuid();
        _permissions.HasProjectPermissionAsync(_actorId, missing, ProjectPermission.ManageUsers).Returns(true);
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.RemoveUserAsync(missing, Guid.NewGuid(), _actorId, "alice"));

        GrantManageUsers();
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.RemoveUserAsync(_project.Id, Guid.NewGuid(), _actorId, "alice"));
    }

    #endregion

    #region List

    [Fact]
    public async Task GetProjectUsers_maps_filters_and_paginates()
    {
        _permissions.IsProjectMemberAsync(_actorId, _project.Id).Returns(true);
        var alice = GivenUser(username: "alice", fullName: "Alice Smith");
        var bob = GivenUser(username: "bob", fullName: "Bob Smith");
        var carol = GivenUser(username: "carol", fullName: "Carol Jones");
        var ghost = new ProjectUser { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), ProjectId = _project.Id };
        _projectUsers.GetByProjectIdAsync(_project.Id).Returns(
        [
            new ProjectUser { Id = Guid.NewGuid(), UserId = alice.Id, ProjectId = _project.Id },
            new ProjectUser { Id = Guid.NewGuid(), UserId = bob.Id, ProjectId = _project.Id },
            new ProjectUser { Id = Guid.NewGuid(), UserId = carol.Id, ProjectId = _project.Id },
            ghost
        ]);

        var result = await _sut.GetProjectUsersAsync(_project.Id, _actorId, " SMITH ", page: 2, pageSize: 1);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal("bob", Assert.Single(result.Items).Username);
    }

    [Fact]
    public async Task GetProjectUsers_without_search_returns_all_members()
    {
        _permissions.IsProjectMemberAsync(_actorId, _project.Id).Returns(true);
        var user = GivenUser();
        _projectUsers.GetByProjectIdAsync(_project.Id).Returns([new ProjectUser { UserId = user.Id, ProjectId = _project.Id }]);

        var result = await _sut.GetProjectUsersAsync(_project.Id, _actorId);

        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task GetProjectUsers_for_non_member_is_forbidden()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => _sut.GetProjectUsersAsync(_project.Id, _actorId));
    }

    #endregion

    #region Update permissions

    private (User user, ProjectUser membership) GivenMemberWithPermissions()
    {
        var user = GivenUser();
        var membership = new ProjectUser { Id = Guid.NewGuid(), ProjectId = _project.Id, UserId = user.Id };
        _projectUsers.GetByUserAndProjectWithPermissionsAsync(user.Id, _project.Id).Returns(membership);
        return (user, membership);
    }

    [Fact]
    public async Task UpdatePermissions_replaces_all_permissions_in_transaction()
    {
        GrantManageUsers();
        var (user, membership) = GivenMemberWithPermissions();
        var dto = new UpdateUserPermissionsDto
        {
            ProjectPermissions = [ProjectPermission.ViewApiKey],
            ScopePermissions = new() { [_scope.Id] = [ScopePermission.UpdateFeatureFlags] }
        };

        var result = await _sut.UpdateUserPermissionsAsync(_project.Id, user.Id, dto, _actorId, "alice");

        Assert.Equal(user.Id, result.UserId);
        Received.InOrder(() =>
        {
            _uow.BeginTransactionAsync();
            _projectUsers.RemoveAllProjectPermissionsAsync(membership.Id);
            _projectUsers.RemoveAllScopePermissionsAsync(membership.Id);
            _projectUsers.AddProjectPermissionsAsync(membership.Id, dto.ProjectPermissions);
            _projectUsers.AddScopePermissionsAsync(membership.Id, Arg.Any<IReadOnlyDictionary<Guid, IEnumerable<ScopePermission>>>());
            _uow.CommitAsync();
        });
        _audit.Received(1).LogProjectAudit("proj", "alice", "ProjectMember", null, "PermissionsUpdated", Arg.Any<object>(), Arg.Any<object>());
    }

    [Fact]
    public async Task UpdatePermissions_rolls_back_on_failure()
    {
        GrantManageUsers();
        var (user, membership) = GivenMemberWithPermissions();
        _projectUsers.RemoveAllScopePermissionsAsync(membership.Id).ThrowsAsync(new InvalidOperationException());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.UpdateUserPermissionsAsync(_project.Id, user.Id, new UpdateUserPermissionsDto(), _actorId, "alice"));

        await _uow.Received(1).RollbackAsync();
    }

    [Fact]
    public async Task UpdatePermissions_cannot_drop_own_manage_users()
    {
        GrantManageUsers();
        var self = new User { Id = _actorId, Username = "alice", FullName = "Alice", IsActive = true };
        _users.GetByIdAsync(_actorId).Returns(self);
        _projectUsers.GetByUserAndProjectWithPermissionsAsync(_actorId, _project.Id)
            .Returns(new ProjectUser { Id = Guid.NewGuid(), UserId = _actorId, ProjectId = _project.Id });

        await Assert.ThrowsAsync<BadRequestException>(() => _sut.UpdateUserPermissionsAsync(_project.Id, _actorId,
            new UpdateUserPermissionsDto { ProjectPermissions = [ProjectPermission.ViewApiKey] }, _actorId, "alice"));
    }

    [Fact]
    public async Task UpdatePermissions_validation_failures()
    {
        var dto = new UpdateUserPermissionsDto();
        await Assert.ThrowsAsync<ForbiddenException>(() => _sut.UpdateUserPermissionsAsync(_project.Id, Guid.NewGuid(), dto, _actorId, "alice"));

        var missing = Guid.NewGuid();
        _permissions.HasProjectPermissionAsync(_actorId, missing, ProjectPermission.ManageUsers).Returns(true);
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.UpdateUserPermissionsAsync(missing, Guid.NewGuid(), dto, _actorId, "alice"));

        GrantManageUsers();
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.UpdateUserPermissionsAsync(_project.Id, Guid.NewGuid(), dto, _actorId, "alice"));

        var orphanId = Guid.NewGuid();
        _projectUsers.GetByUserAndProjectWithPermissionsAsync(orphanId, _project.Id).Returns(new ProjectUser { UserId = orphanId });
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.UpdateUserPermissionsAsync(_project.Id, orphanId, dto, _actorId, "alice"));
    }

    #endregion
}
