using Flare.Application.Audit;
using Flare.Application.DTOs;
using Flare.Application.Interfaces;
using Flare.Domain.Entities;
using Flare.Domain.Enums;
using Flare.Domain.Exceptions;
using Flare.Infrastructure.Data.Repositories.Interfaces;

namespace Flare.Application.Services;

public class SegmentService : ISegmentService
{
    private readonly ISegmentRepository _segmentRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IPermissionService _permissionService;
    private readonly IAuditLogger _auditLogger;

    public SegmentService(
        ISegmentRepository segmentRepository,
        IProjectRepository projectRepository,
        IPermissionService permissionService,
        IAuditLogger auditLogger)
    {
        _segmentRepository = segmentRepository;
        _projectRepository = projectRepository;
        _permissionService = permissionService;
        _auditLogger = auditLogger;
    }

    public async Task<List<SegmentResponseDto>> GetByProjectIdAsync(Guid projectId, Guid currentUserId)
    {
        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, projectId, ProjectPermission.ViewSegments))
            throw new ForbiddenException("You do not have permission to view segments in this project.");

        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
            throw new NotFoundException("Project not found.");

        var segments = await _segmentRepository.GetByProjectIdAsync(projectId);
        var members = new Dictionary<Guid, int>();

        foreach (var s in segments)
        {
            var segmentWithMembers = await _segmentRepository.GetByIdWithMembersAsync(s.Id);
            members[s.Id] = segmentWithMembers?.Members.Count ?? 0;
        }

        return segments.Select(s => MapToResponseDto(s, members.GetValueOrDefault(s.Id))).ToList();
    }

    public async Task<SegmentDetailResponseDto> GetByIdAsync(Guid segmentId, Guid currentUserId)
    {
        var segment = await _segmentRepository.GetByIdWithMembersAsync(segmentId);
        if (segment == null)
            throw new NotFoundException("Segment not found.");

        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, segment.ProjectId, ProjectPermission.ViewSegments))
            throw new ForbiddenException("You do not have permission to view segments in this project.");

        return MapToDetailResponseDto(segment);
    }

    public async Task CreateAsync(Guid projectId, CreateSegmentDto dto, Guid currentUserId, string actorUsername)
    {
        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, projectId, ProjectPermission.ManageSegments))
            throw new ForbiddenException("You do not have permission to manage segments in this project.");

        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
            throw new NotFoundException("Project not found.");

        if (await _segmentRepository.ExistsByProjectAndNameAsync(projectId, dto.Name))
            throw new BadRequestException($"A segment named '{dto.Name}' already exists in this project.");

        var segment = new Segment
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = dto.Name,
            Description = dto.Description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _segmentRepository.AddAsync(segment);

        _auditLogger.LogProjectAudit(project.Alias, actorUsername, "Segment", null, "Created");
    }

    public async Task UpdateAsync(Guid segmentId, UpdateSegmentDto dto, Guid currentUserId, string actorUsername)
    {
        var segment = await _segmentRepository.GetByIdWithMembersAsync(segmentId);
        if (segment == null)
            throw new NotFoundException("Segment not found.");

        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, segment.ProjectId, ProjectPermission.ManageSegments))
            throw new ForbiddenException("You do not have permission to manage segments in this project.");

        if (segment.Name != dto.Name && await _segmentRepository.ExistsByProjectAndNameAsync(segment.ProjectId, dto.Name, excludeSegmentId: segmentId))
            throw new BadRequestException($"A segment named '{dto.Name}' already exists in this project.");

        segment.Name = dto.Name;
        segment.Description = dto.Description;
        segment.UpdatedAt = DateTime.UtcNow;

        await _segmentRepository.UpdateAsync(segment);

        _auditLogger.LogProjectAudit(segment.Project.Alias, actorUsername, "Segment", null, "Updated");
    }

    public async Task DeleteAsync(Guid segmentId, Guid currentUserId, string actorUsername)
    {
        var segment = await _segmentRepository.GetByIdWithMembersAsync(segmentId);
        if (segment == null)
            throw new NotFoundException("Segment not found.");

        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, segment.ProjectId, ProjectPermission.ManageSegments))
            throw new ForbiddenException("You do not have permission to manage segments in this project.");

        await _segmentRepository.DeleteAsync(segmentId);

        _auditLogger.LogProjectAudit(segment.Project.Alias, actorUsername, "Segment", null, "Deleted");
    }

    public async Task<List<SegmentMemberResponseDto>> GetMembersAsync(Guid segmentId, Guid currentUserId)
    {
        var segment = await _segmentRepository.GetByIdAsync(segmentId);
        if (segment == null)
            throw new NotFoundException("Segment not found.");

        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, segment.ProjectId, ProjectPermission.ViewSegments))
            throw new ForbiddenException("You do not have permission to view segments in this project.");

        var members = await _segmentRepository.GetMembersBySegmentIdAsync(segmentId);
        return members.Select(MapMemberToDto).ToList();
    }

    public async Task AddMembersAsync(Guid segmentId, AddSegmentMembersDto dto, Guid currentUserId, string actorUsername)
    {
        var segment = await _segmentRepository.GetByIdWithMembersAsync(segmentId);
        if (segment == null)
            throw new NotFoundException("Segment not found.");

        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, segment.ProjectId, ProjectPermission.ManageSegments))
            throw new ForbiddenException("You do not have permission to manage segments in this project.");

        var uniqueKeys = dto.TargetingKeys.Distinct(StringComparer.Ordinal).ToList();
        var newMembers = new List<SegmentMember>();

        foreach (var key in uniqueKeys)
        {
            if (!await _segmentRepository.MemberExistsAsync(segmentId, key))
            {
                newMembers.Add(new SegmentMember
                {
                    Id = Guid.NewGuid(),
                    SegmentId = segmentId,
                    TargetingKey = key
                });
            }
        }

        if (newMembers.Count > 0)
            await _segmentRepository.AddMembersAsync(newMembers);

        _auditLogger.LogProjectAudit(segment.Project.Alias, actorUsername, "SegmentMember", segment.Name, "Added");

        var allMembers = await _segmentRepository.GetMembersBySegmentIdAsync(segmentId);
    }

    public async Task DeleteMemberAsync(Guid segmentId, string targetingKey, Guid currentUserId, string actorUsername)
    {
        var segment = await _segmentRepository.GetByIdWithMembersAsync(segmentId);
        if (segment == null)
            throw new NotFoundException("Segment not found.");

        if (!await _permissionService.HasProjectPermissionAsync(currentUserId, segment.ProjectId, ProjectPermission.ManageSegments))
            throw new ForbiddenException("You do not have permission to manage segments in this project.");

        if (!await _segmentRepository.MemberExistsAsync(segmentId, targetingKey))
            throw new NotFoundException($"Member '{targetingKey}' not found in segment.");

        await _segmentRepository.DeleteMemberByKeyAsync(segmentId, targetingKey);

        _auditLogger.LogProjectAudit(segment.Project.Alias, actorUsername, "SegmentMember", segment.Name, "Deleted");
    }

    #region Helpers

    private static SegmentResponseDto MapToResponseDto(Segment segment, int memberCount)
    {
        return new SegmentResponseDto
        {
            Id = segment.Id,
            ProjectId = segment.ProjectId,
            Name = segment.Name,
            Description = segment.Description,
            MemberCount = memberCount,
            CreatedAt = segment.CreatedAt
        };
    }

    private static SegmentDetailResponseDto MapToDetailResponseDto(Segment segment)
    {
        return new SegmentDetailResponseDto
        {
            Id = segment.Id,
            ProjectId = segment.ProjectId,
            Name = segment.Name,
            Description = segment.Description,
            MemberCount = segment.Members.Count,
            CreatedAt = segment.CreatedAt,
            UpdatedAt = segment.UpdatedAt,
            Members = segment.Members.Select(MapMemberToDto).ToList()
        };
    }

    private static SegmentMemberResponseDto MapMemberToDto(SegmentMember member)
    {
        return new SegmentMemberResponseDto
        {
            Id = member.Id,
            SegmentId = member.SegmentId,
            TargetingKey = member.TargetingKey
        };
    }

    #endregion
}
