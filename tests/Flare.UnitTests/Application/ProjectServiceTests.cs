using Flare.Application.Audit;
using Flare.Application.DTOs;
using Flare.Application.Interfaces;
using Flare.Application.Services;
using Flare.Domain.Entities;
using Flare.Domain.Enums;
using Flare.Domain.Exceptions;
using Flare.Infrastructure.Data.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Hybrid;
using NSubstitute;

namespace Flare.UnitTests.Application;

public class ProjectServiceTests
{
    private readonly IProjectRepository _projects = Substitute.For<IProjectRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPermissionService _permissions = Substitute.For<IPermissionService>();
    private readonly HybridCache _cache = Substitute.For<HybridCache>();
    private readonly IAuditLogger _audit = Substitute.For<IAuditLogger>();
    private readonly ProjectService _sut;

    private readonly Guid _userId = Guid.NewGuid();

    public ProjectServiceTests()
    {
        _sut = new ProjectService(_projects, _users, _permissions, _cache, _audit);
    }

    private Project GivenProject(bool archived = false)
    {
        var project = new Project { Id = Guid.NewGuid(), Alias = "proj", Name = "Project", ApiKey = "key", IsArchived = archived };
        _projects.GetByIdAsync(project.Id).Returns(project);
        return project;
    }

    private void Grant(Guid projectId, ProjectPermission permission) =>
        _permissions.HasProjectPermissionAsync(_userId, projectId, permission).Returns(true);

    [Fact]
    public async Task Create_builds_default_scopes_and_owner_membership()
    {
        Project? saved = null;
        await _projects.AddAsync(Arg.Do<Project>(p => saved = p));

        await _sut.CreateAsync(new CreateProjectDto { Alias = "new", Name = "New project", Description = "d" }, _userId, "alice");

        Assert.NotNull(saved);
        Assert.Equal("new", saved.Alias);
        Assert.Equal(_userId, saved.CreatedBy);
        Assert.False(string.IsNullOrWhiteSpace(saved.ApiKey));
        Assert.DoesNotContain('+', saved.ApiKey);
        Assert.DoesNotContain('/', saved.ApiKey);
        Assert.DoesNotContain('=', saved.ApiKey);
        Assert.Equal(["dev", "staging", "production"], saved.Scopes.OrderBy(s => s.Index).Select(s => s.Alias));

        var owner = Assert.Single(saved.Members);
        Assert.Equal(_userId, owner.UserId);
        Assert.Equal(Enum.GetValues<ProjectPermission>(), owner.ProjectPermissions.Select(p => p.Permission).Order());
        Assert.Equal(6, owner.ScopePermissions.Count);
        _audit.Received(1).LogProjectAudit("new", "alice", "Project", null, "Created");
    }

    [Fact]
    public async Task Create_duplicate_alias_throws()
    {
        _projects.ExistsByAliasAsync("dup").Returns(true);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.CreateAsync(new CreateProjectDto { Alias = "dup", Name = "Dup" }, _userId, "alice"));
    }

    [Fact]
    public async Task Update_changes_fields_and_invalidates_previous_alias_tag()
    {
        var project = GivenProject();
        Grant(project.Id, ProjectPermission.ManageProjectSettings);

        await _sut.UpdateAsync(project.Id, new UpdateProjectDto { Alias = "renamed", Name = "Renamed", Description = "x" }, _userId, "alice");

        Assert.Equal("renamed", project.Alias);
        Assert.Equal("Renamed", project.Name);
        await _projects.Received(1).UpdateAsync(project);
        await _cache.Received(1).RemoveByTagAsync("tag_proj", Arg.Any<CancellationToken>());
        _audit.Received(1).LogProjectAudit("renamed", "alice", "Project", null, "Updated", Arg.Any<object>(), Arg.Any<object>());
    }

    [Fact]
    public async Task Delete_removes_project_and_invalidates_cache()
    {
        var project = GivenProject();
        Grant(project.Id, ProjectPermission.DeleteProject);

        await _sut.DeleteAsync(project.Id, _userId, "alice");

        await _projects.Received(1).DeleteAsync(project.Id);
        await _cache.Received(1).RemoveByTagAsync("tag_proj", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegenerateApiKey_replaces_key()
    {
        var project = GivenProject();
        Grant(project.Id, ProjectPermission.RegenerateApiKey);

        await _sut.RegenerateApiKeyAsync(project.Id, _userId, "alice");

        Assert.NotEqual("key", project.ApiKey);
        Assert.Equal(43, project.ApiKey.Length);
        await _cache.Received(1).RemoveByTagAsync("tag_proj", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetApiKey_returns_key()
    {
        var project = GivenProject();
        Grant(project.Id, ProjectPermission.ViewApiKey);

        var result = await _sut.GetApiKeyAsync(project.Id, _userId);

        Assert.Equal("key", result.ApiKey);
    }

    [Fact]
    public async Task Archive_and_unarchive_toggle_state()
    {
        var project = GivenProject();
        Grant(project.Id, ProjectPermission.ManageProjectSettings);

        await _sut.ArchiveAsync(project.Id, _userId, "alice");
        Assert.True(project.IsArchived);
        await Assert.ThrowsAsync<BadRequestException>(() => _sut.ArchiveAsync(project.Id, _userId, "alice"));

        await _sut.UnarchiveAsync(project.Id, _userId, "alice");
        Assert.False(project.IsArchived);
        await Assert.ThrowsAsync<BadRequestException>(() => _sut.UnarchiveAsync(project.Id, _userId, "alice"));
    }

    public static TheoryData<string> PermissionGuardedOperations =>
        new() { "update", "delete", "apikey", "regenerate", "archive", "unarchive" };

    [Theory]
    [MemberData(nameof(PermissionGuardedOperations))]
    public async Task Operations_without_permission_are_forbidden(string operation)
    {
        var project = GivenProject();

        await Assert.ThrowsAsync<ForbiddenException>(Invoke(operation, project.Id));
        await _projects.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
        await _projects.DidNotReceiveWithAnyArgs().DeleteAsync(default);
    }

    [Theory]
    [MemberData(nameof(PermissionGuardedOperations))]
    public async Task Operations_on_missing_project_throw_not_found(string operation)
    {
        var projectId = Guid.NewGuid();
        _permissions.HasProjectPermissionAsync(_userId, projectId, Arg.Any<ProjectPermission>()).Returns(true);

        await Assert.ThrowsAsync<NotFoundException>(Invoke(operation, projectId));
    }

    private Func<Task> Invoke(string operation, Guid projectId) => operation switch
    {
        "update" => () => _sut.UpdateAsync(projectId, new UpdateProjectDto { Alias = "a", Name = "abc" }, _userId, "alice"),
        "delete" => () => _sut.DeleteAsync(projectId, _userId, "alice"),
        "apikey" => () => _sut.GetApiKeyAsync(projectId, _userId),
        "regenerate" => () => _sut.RegenerateApiKeyAsync(projectId, _userId, "alice"),
        "archive" => () => _sut.ArchiveAsync(projectId, _userId, "alice"),
        _ => () => _sut.UnarchiveAsync(projectId, _userId, "alice")
    };

    [Fact]
    public async Task GetById_maps_project_or_throws()
    {
        var project = GivenProject(archived: true);

        var result = await _sut.GetByIdAsync(project.Id, _userId);

        Assert.Equal(project.Id, result.Id);
        Assert.Equal("proj", result.Alias);
        Assert.True(result.IsArchived);
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetByIdAsync(Guid.NewGuid(), _userId));
    }

    [Theory]
    [InlineData(GlobalRole.Admin)]
    [InlineData(GlobalRole.SuperAdmin)]
    public async Task GetUserProjects_for_admin_returns_all_projects(GlobalRole role)
    {
        _users.GetByIdAsync(_userId).Returns(new User { Id = _userId, GlobalRole = role });
        _projects.GetAllAsync().Returns([new Project { Alias = "a" }, new Project { Alias = "b" }]);

        var result = await _sut.GetUserProjectsAsync(_userId);

        Assert.Equal(2, result.Count);
        await _projects.DidNotReceive().GetByUserIdAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task GetUserProjects_for_regular_user_returns_memberships()
    {
        _users.GetByIdAsync(_userId).Returns(new User { Id = _userId, GlobalRole = GlobalRole.User });
        _projects.GetByUserIdAsync(_userId).Returns([new Project { Alias = "mine" }]);

        var result = await _sut.GetUserProjectsAsync(_userId);

        Assert.Equal("mine", Assert.Single(result).Alias);
    }

    [Fact]
    public async Task GetUserProjects_unknown_user_throws()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetUserProjectsAsync(_userId));
    }

    [Fact]
    public async Task GetMyPermissions_returns_project_and_scope_permissions()
    {
        var project = GivenProject();
        _permissions.IsProjectMemberAsync(_userId, project.Id).Returns(true);
        _permissions.GetUserProjectPermissionsAsync(_userId, project.Id).Returns([ProjectPermission.ViewApiKey]);
        var scopePermissions = new Dictionary<Guid, List<ScopePermission>> { [Guid.NewGuid()] = [ScopePermission.ReadFeatureFlags] };
        _permissions.GetUserScopePermissionsAsync(_userId, project.Id).Returns(scopePermissions);

        var result = await _sut.GetMyPermissionsAsync(project.Id, _userId);

        Assert.Equal(_userId, result.UserId);
        Assert.Equal([ProjectPermission.ViewApiKey], result.ProjectPermissions);
        Assert.Same(scopePermissions, result.ScopePermissions);
    }

    [Fact]
    public async Task GetMyPermissions_requires_existing_project_and_membership()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetMyPermissionsAsync(Guid.NewGuid(), _userId));

        var project = GivenProject();
        await Assert.ThrowsAsync<ForbiddenException>(() => _sut.GetMyPermissionsAsync(project.Id, _userId));
    }
}
