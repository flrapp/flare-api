namespace Flare.Domain.Entities;

public class SegmentMember
{
    public Guid Id { get; set; }
    public Guid SegmentId { get; set; }
    public Segment Segment { get; set; } = null!;
    public string TargetingKey { get; set; } = string.Empty;
}
