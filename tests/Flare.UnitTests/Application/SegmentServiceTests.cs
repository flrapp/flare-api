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

public class SegmentServiceTests
{
    private readonly ISegmentRepository _segments = Substitute.For<ISegmentRepository>();
    private readonly IProjectRepository _projects = Substitute.For<IProjectRepository>();
    private readonly IPermissionService _permissions = Substitute.For<IPermissionService>();
    private readonly IAuditLogger _audit = Substitute.For<IAuditLogger>();
    private readonly SegmentService _sut;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Project _project = new() { Id = Guid.NewGuid(), Alias = "proj" };

    public SegmentServiceTests()
    {
        _sut = new SegmentService(_segments, _projects, _permissions, _audit);
        _projects.GetByIdAsync(_project.Id).Returns(_project);
    }

    private void GrantManageSegments() =>
        _permissions.HasProjectPermissionAsync(_userId, _project.Id, ProjectPermission.ManageSegments).Returns(true);

    private Segment GivenSegment(params string[] memberKeys)
    {
        var segment = new Segment { Id = Guid.NewGuid(), ProjectId = _project.Id, Project = _project, Name = "beta" };
        foreach (var key in memberKeys)
            segment.Members.Add(new SegmentMember { Id = Guid.NewGuid(), SegmentId = segment.Id, TargetingKey = key });
        _segments.GetByIdAsync(segment.Id).Returns(segment);
        _segments.GetByIdWithMembersAsync(segment.Id).Returns(segment);
        return segment;
    }

    [Fact]
    public async Task GetByProjectId_returns_segments_with_member_counts()
    {
        GrantManageSegments();
        var withMembers = GivenSegment("u1", "u2");
        var orphan = new Segment { Id = Guid.NewGuid(), ProjectId = _project.Id, Name = "gone" };
        _segments.GetByProjectIdAsync(_project.Id).Returns([withMembers, orphan]);

        var result = await _sut.GetByProjectIdAsync(_project.Id, _userId);

        Assert.Equal(2, result.Single(s => s.Id == withMembers.Id).MemberCount);
        Assert.Equal(0, result.Single(s => s.Id == orphan.Id).MemberCount);
    }

    [Fact]
    public async Task GetByProjectId_requires_permission_and_project()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => _sut.GetByProjectIdAsync(_project.Id, _userId));

        var missing = Guid.NewGuid();
        _permissions.HasProjectPermissionAsync(_userId, missing, ProjectPermission.ManageSegments).Returns(true);
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetByProjectIdAsync(missing, _userId));
    }

    [Fact]
    public async Task Create_adds_segment()
    {
        GrantManageSegments();

        await _sut.CreateAsync(_project.Id, new CreateSegmentDto { Name = "vip", Description = "d" }, _userId, "alice");

        await _segments.Received(1).AddAsync(Arg.Is<Segment>(s => s.Name == "vip" && s.ProjectId == _project.Id));
        _audit.Received(1).LogProjectAudit("proj", "alice", "Segment", null, "Created");
    }

    [Fact]
    public async Task Create_duplicate_name_is_rejected()
    {
        GrantManageSegments();
        _segments.ExistsByProjectAndNameAsync(_project.Id, "vip").Returns(true);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.CreateAsync(_project.Id, new CreateSegmentDto { Name = "vip" }, _userId, "alice"));
    }

    [Fact]
    public async Task Create_requires_permission_and_project()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _sut.CreateAsync(_project.Id, new CreateSegmentDto { Name = "vip" }, _userId, "alice"));

        var missing = Guid.NewGuid();
        _permissions.HasProjectPermissionAsync(_userId, missing, ProjectPermission.ManageSegments).Returns(true);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.CreateAsync(missing, new CreateSegmentDto { Name = "vip" }, _userId, "alice"));
    }

    [Fact]
    public async Task Update_renames_segment()
    {
        GrantManageSegments();
        var segment = GivenSegment();

        await _sut.UpdateAsync(segment.Id, new UpdateSegmentDto { Name = "gamma", Description = "new" }, _userId, "alice");

        Assert.Equal("gamma", segment.Name);
        Assert.Equal("new", segment.Description);
        await _segments.Received(1).UpdateAsync(segment);
    }

    [Fact]
    public async Task Update_with_same_name_skips_uniqueness_check()
    {
        GrantManageSegments();
        var segment = GivenSegment();

        await _sut.UpdateAsync(segment.Id, new UpdateSegmentDto { Name = "beta" }, _userId, "alice");

        await _segments.DidNotReceiveWithAnyArgs().ExistsByProjectAndNameAsync(default, default!, default);
    }

    [Fact]
    public async Task Update_to_taken_name_is_rejected()
    {
        GrantManageSegments();
        var segment = GivenSegment();
        _segments.ExistsByProjectAndNameAsync(_project.Id, "taken", segment.Id).Returns(true);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.UpdateAsync(segment.Id, new UpdateSegmentDto { Name = "taken" }, _userId, "alice"));
    }

    [Fact]
    public async Task Delete_removes_segment()
    {
        GrantManageSegments();
        var segment = GivenSegment();

        await _sut.DeleteAsync(segment.Id, _userId, "alice");

        await _segments.Received(1).DeleteAsync(segment.Id);
    }

    [Fact]
    public async Task GetMembers_pages_through_repository()
    {
        GrantManageSegments();
        var segment = GivenSegment();
        var member = new SegmentMember { Id = Guid.NewGuid(), SegmentId = segment.Id, TargetingKey = "u9" };
        _segments.GetMembersPagedAsync(segment.Id, 10, 10, "u").Returns(([member], 11));

        var result = await _sut.GetMembersAsync(segment.Id, _userId, "u", page: 2, pageSize: 10);

        Assert.Equal(11, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal("u9", Assert.Single(result.Items).TargetingKey);
    }

    [Fact]
    public async Task AddMembers_skips_duplicates_and_existing_keys()
    {
        GrantManageSegments();
        var segment = GivenSegment();
        _segments.MemberExistsAsync(segment.Id, "existing").Returns(true);

        await _sut.AddMembersAsync(segment.Id, new AddSegmentMembersDto { TargetingKeys = ["a", "a", "existing", "b"] }, _userId, "alice");

        await _segments.Received(1).AddMembersAsync(Arg.Is<IEnumerable<SegmentMember>>(m =>
            m.Select(x => x.TargetingKey).SequenceEqual(new[] { "a", "b" })));
        _audit.Received(1).LogProjectAudit("proj", "alice", "SegmentMember", "beta", "Added");
    }

    [Fact]
    public async Task AddMembers_with_only_existing_keys_writes_nothing()
    {
        GrantManageSegments();
        var segment = GivenSegment();
        _segments.MemberExistsAsync(segment.Id, "existing").Returns(true);

        await _sut.AddMembersAsync(segment.Id, new AddSegmentMembersDto { TargetingKeys = ["existing"] }, _userId, "alice");

        await _segments.DidNotReceiveWithAnyArgs().AddMembersAsync(default!);
    }

    [Fact]
    public async Task DeleteMember_removes_existing_key()
    {
        GrantManageSegments();
        var segment = GivenSegment();
        _segments.MemberExistsAsync(segment.Id, "u1").Returns(true);

        await _sut.DeleteMemberAsync(segment.Id, "u1", _userId, "alice");

        await _segments.Received(1).DeleteMemberByKeyAsync(segment.Id, "u1");
    }

    [Fact]
    public async Task DeleteMember_unknown_key_throws_not_found()
    {
        GrantManageSegments();
        var segment = GivenSegment();

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.DeleteMemberAsync(segment.Id, "nobody", _userId, "alice"));
    }

    public static TheoryData<string> SegmentOperations => new() { "update", "delete", "members", "add", "remove" };

    [Theory]
    [MemberData(nameof(SegmentOperations))]
    public async Task Operations_on_missing_segment_throw_not_found(string operation)
    {
        await Assert.ThrowsAsync<NotFoundException>(Invoke(operation, Guid.NewGuid()));
    }

    [Theory]
    [MemberData(nameof(SegmentOperations))]
    public async Task Operations_without_permission_are_forbidden(string operation)
    {
        var segment = GivenSegment();

        await Assert.ThrowsAsync<ForbiddenException>(Invoke(operation, segment.Id));
    }

    private Func<Task> Invoke(string operation, Guid segmentId) => operation switch
    {
        "update" => () => _sut.UpdateAsync(segmentId, new UpdateSegmentDto { Name = "x" }, _userId, "alice"),
        "delete" => () => _sut.DeleteAsync(segmentId, _userId, "alice"),
        "members" => () => _sut.GetMembersAsync(segmentId, _userId),
        "add" => () => _sut.AddMembersAsync(segmentId, new AddSegmentMembersDto { TargetingKeys = ["k"] }, _userId, "alice"),
        _ => () => _sut.DeleteMemberAsync(segmentId, "k", _userId, "alice")
    };
}
