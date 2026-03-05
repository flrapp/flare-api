using Flare.Domain.Entities;

namespace Flare.Infrastructure.Data.Repositories.Interfaces;

public interface ISegmentRepository
{
    Task<List<Segment>> GetByProjectIdAsync(Guid projectId);
    Task<Segment?> GetByIdAsync(Guid segmentId);
    Task<Segment?> GetByIdWithMembersAsync(Guid segmentId);
    Task<bool> ExistsByProjectAndNameAsync(Guid projectId, string name, Guid? excludeSegmentId = null);
    Task AddAsync(Segment segment);
    Task UpdateAsync(Segment segment);
    Task DeleteAsync(Guid segmentId);

    Task<List<SegmentMember>> GetMembersBySegmentIdAsync(Guid segmentId);
    Task<bool> MemberExistsAsync(Guid segmentId, string targetingKey);
    Task<bool> IsTargetingKeyInSegmentAsync(Guid segmentId, string targetingKey);
    Task AddMembersAsync(IEnumerable<SegmentMember> members);
    Task DeleteMemberByKeyAsync(Guid segmentId, string targetingKey);
}
