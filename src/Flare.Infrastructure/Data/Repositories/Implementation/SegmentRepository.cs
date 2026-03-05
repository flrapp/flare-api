using Flare.Domain.Entities;
using Flare.Infrastructure.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Flare.Infrastructure.Data.Repositories.Implementation;

public class SegmentRepository : ISegmentRepository
{
    private readonly ApplicationDbContext _context;

    public SegmentRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Segment>> GetByProjectIdAsync(Guid projectId)
    {
        return await _context.Segments
            .Where(s => s.ProjectId == projectId)
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<Segment?> GetByIdAsync(Guid segmentId)
    {
        return await _context.Segments.FindAsync(segmentId);
    }

    public async Task<Segment?> GetByIdWithMembersAsync(Guid segmentId)
    {
        return await _context.Segments
            .Include(s => s.Members)
            .Include(s => s.Project)
            .FirstOrDefaultAsync(s => s.Id == segmentId);
    }

    public async Task<bool> ExistsByProjectAndNameAsync(Guid projectId, string name, Guid? excludeSegmentId = null)
    {
        var query = _context.Segments.Where(s => s.ProjectId == projectId && s.Name == name);
        if (excludeSegmentId.HasValue)
            query = query.Where(s => s.Id != excludeSegmentId.Value);
        return await query.AnyAsync();
    }

    public async Task AddAsync(Segment segment)
    {
        _context.Segments.Add(segment);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Segment segment)
    {
        _context.Segments.Update(segment);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid segmentId)
    {
        var segment = await _context.Segments.FindAsync(segmentId);
        if (segment != null)
        {
            _context.Segments.Remove(segment);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<SegmentMember>> GetMembersBySegmentIdAsync(Guid segmentId)
    {
        return await _context.SegmentMembers
            .Where(m => m.SegmentId == segmentId)
            .OrderBy(m => m.TargetingKey)
            .ToListAsync();
    }

    public async Task<bool> MemberExistsAsync(Guid segmentId, string targetingKey)
    {
        return await _context.SegmentMembers
            .AnyAsync(m => m.SegmentId == segmentId && m.TargetingKey == targetingKey);
    }

    public async Task<bool> IsTargetingKeyInSegmentAsync(Guid segmentId, string targetingKey)
    {
        return await _context.SegmentMembers
            .AnyAsync(m => m.SegmentId == segmentId && m.TargetingKey == targetingKey);
    }

    public async Task AddMembersAsync(IEnumerable<SegmentMember> members)
    {
        _context.SegmentMembers.AddRange(members);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteMemberByKeyAsync(Guid segmentId, string targetingKey)
    {
        var member = await _context.SegmentMembers
            .FirstOrDefaultAsync(m => m.SegmentId == segmentId && m.TargetingKey == targetingKey);
        if (member != null)
        {
            _context.SegmentMembers.Remove(member);
            await _context.SaveChangesAsync();
        }
    }
}
