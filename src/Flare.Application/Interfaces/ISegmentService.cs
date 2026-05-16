using Flare.Application.DTOs;

namespace Flare.Application.Interfaces;

public interface ISegmentService
{
    Task<List<SegmentResponseDto>> GetByProjectIdAsync(Guid projectId, Guid currentUserId);
    Task CreateAsync(Guid projectId, CreateSegmentDto dto, Guid currentUserId, string actorUsername);
    Task UpdateAsync(Guid segmentId, UpdateSegmentDto dto, Guid currentUserId, string actorUsername);
    Task DeleteAsync(Guid segmentId, Guid currentUserId, string actorUsername);

    Task<PagedResult<SegmentMemberResponseDto>> GetMembersAsync(Guid segmentId, Guid currentUserId, string? search = null, int page = 1, int pageSize = 20);
    Task AddMembersAsync(Guid segmentId, AddSegmentMembersDto dto, Guid currentUserId, string actorUsername);
    Task DeleteMemberAsync(Guid segmentId, string targetingKey, Guid currentUserId, string actorUsername);
}
