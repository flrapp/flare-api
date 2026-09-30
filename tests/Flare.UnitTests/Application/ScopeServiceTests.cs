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

public class ScopeServiceTests
{
    private readonly IScopeRepository _scopes = Substitute.For<IScopeRepository>();
    private readonly IProjectRepository _projects = Substitute.For<IProjectRepository>();
    private readonly IProjectUserRepository _projectUsers = Substitute.For<IProjectUserRepository>();
    private readonly IFeatureFlagRepository _flags = Substitute.For<IFeatureFlagRepository>();
    private readonly IPermissionService _permissions = Substitute.For<IPermissionService>();
    private readonly HybridCache _cache = Substitute.For<HybridCache>();
    private readonly IAuditLogger _audit = Substitute.For<IAuditLogger>();
    private readonly ScopeService _sut;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Project _project = new() { Id = Guid.NewGuid(), Alias = "proj" };

    public ScopeServiceTests()
    {
        _sut = new ScopeService(_scopes, _projects, _projectUsers, _flags, _permissions, _cache, _audit);
        _projects.GetByIdAsync(_project.Id).Returns(_project);
        _flags.GetByProjectIdAsync(Arg.Any<Guid>()).Returns([]);
    }

    private void GrantManageScopes() =>
        _permissions.HasProjectPermissionAsync(_userId, _project.Id, ProjectPermission.ManageScopes).Returns(true);

    private Scope GivenScope()
    {
        var scope = new Scope { Id = Guid.NewGuid(), ProjectId = _project.Id, Project = _project, Alias = "dev", Name = "Dev" };
        _scopes.GetByIdWithProjectAsync(scope.Id).Returns(scope);
        return scope;
    }

    [Fact]
    public async Task Create_adds_scope_and_values_for_existing_flags()
    {
        GrantManageScopes();
        _scopes.GetByProjectIdAsync(_project.Id).Returns([new Scope { Index = 0 }, new Scope { Index = 4 }]);
        var flag = new FeatureFlag { Id = Guid.NewGuid(), Type = FeatureFlagType.String };
        _flags.GetByProjectIdAsync(_project.Id).Returns([flag]);
        Scope? added = null;
        await _scopes.AddAsync(Arg.Do<Scope>(s => added = s));

        await _sut.CreateAsync(_project.Id, new CreateScopeDto { Alias = "qa", Name = "QA" }, _userId, "alice");

        Assert.NotNull(added);
        Assert.Equal("qa", added.Alias);
        Assert.Equal(4, added.Index);
        await _flags.Received(1).AddValuesAsync(Arg.Is<IEnumerable<FeatureFlagValue>>(v =>
            v.Single().FeatureFlagId == flag.Id && v.Single().ScopeId == added.Id));
        _audit.Received(1).LogProjectAudit("proj", "alice", "Scope", "qa", "Created");
    }

    [Fact]
    public async Task Create_in_project_without_flags_or_scopes_starts_at_index_zero()
    {
        GrantManageScopes();
        _scopes.GetByProjectIdAsync(_project.Id).Returns([]);
        Scope? added = null;
        await _scopes.AddAsync(Arg.Do<Scope>(s => added = s));

        await _sut.CreateAsync(_project.Id, new CreateScopeDto { Alias = "qa", Name = "QA" }, _userId, "alice");

        Assert.Equal(0, added!.Index);
        await _flags.DidNotReceiveWithAnyArgs().AddValuesAsync(default!);
    }

    [Fact]
    public async Task Create_without_permission_is_forbidden()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _sut.CreateAsync(_project.Id, new CreateScopeDto { Alias = "qa", Name = "QA" }, _userId, "alice"));
    }

    [Fact]
    public async Task Create_duplicate_alias_is_rejected()
    {
        GrantManageScopes();
        _scopes.ExistsByProjectAndAliasAsync(_project.Id, "dev").Returns(true);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.CreateAsync(_project.Id, new CreateScopeDto { Alias = "dev", Name = "Dev" }, _userId, "alice"));
    }

    [Fact]
    public async Task Create_for_missing_project_throws_not_found()
    {
        var missing = Guid.NewGuid();
        _permissions.HasProjectPermissionAsync(_userId, missing, ProjectPermission.ManageScopes).Returns(true);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.CreateAsync(missing, new CreateScopeDto { Alias = "qa", Name = "QA" }, _userId, "alice"));
    }

    [Fact]
    public async Task Update_changes_fields_and_invalidates_previous_scope_tag()
    {
        GrantManageScopes();
        var scope = GivenScope();

        await _sut.UpdateAsync(scope.Id, new UpdateScopeDto { Alias = "development", Name = "Development", Description = "d" }, _userId, "alice");

        Assert.Equal("development", scope.Alias);
        Assert.Equal("d", scope.Description);
        await _scopes.Received(1).UpdateAsync(scope);
        await _cache.Received(1).RemoveByTagAsync("tag_proj_dev", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_removes_permissions_then_scope_and_invalidates_cache()
    {
        GrantManageScopes();
        var scope = GivenScope();

        await _sut.DeleteAsync(scope.Id, _userId, "alice");

        Received.InOrder(() =>
        {
            _projectUsers.RemoveAllScopePermissionsForScopeAsync(scope.Id);
            _scopes.DeleteAsync(scope.Id);
        });
        await _cache.Received(1).RemoveByTagAsync("tag_proj_dev", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_and_delete_missing_scope_throw_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.UpdateAsync(Guid.NewGuid(), new UpdateScopeDto { Alias = "a" }, _userId, "alice"));
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.DeleteAsync(Guid.NewGuid(), _userId, "alice"));
    }

    [Fact]
    public async Task Update_and_delete_without_permission_are_forbidden()
    {
        var scope = GivenScope();

        await Assert.ThrowsAsync<ForbiddenException>(() => _sut.UpdateAsync(scope.Id, new UpdateScopeDto { Alias = "a" }, _userId, "alice"));
        await Assert.ThrowsAsync<ForbiddenException>(() => _sut.DeleteAsync(scope.Id, _userId, "alice"));
    }

    [Fact]
    public async Task GetByProjectId_maps_scopes_for_members()
    {
        _permissions.IsProjectMemberAsync(_userId, _project.Id).Returns(true);
        _scopes.GetByProjectIdAsync(_project.Id).Returns([new Scope { Id = Guid.NewGuid(), ProjectId = _project.Id, Alias = "dev", Name = "Dev", Description = "d" }]);

        var result = await _sut.GetByProjectIdAsync(_project.Id, _userId);

        var dto = Assert.Single(result);
        Assert.Equal("dev", dto.Alias);
        Assert.Equal("d", dto.Description);
    }

    [Fact]
    public async Task GetByProjectId_for_non_member_is_forbidden()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => _sut.GetByProjectIdAsync(_project.Id, _userId));
    }
}
