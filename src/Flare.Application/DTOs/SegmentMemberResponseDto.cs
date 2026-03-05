namespace Flare.Application.DTOs;

public class SegmentMemberResponseDto
{
    public Guid Id { get; set; }
    public Guid SegmentId { get; set; }
    public string TargetingKey { get; set; } = string.Empty;
}
