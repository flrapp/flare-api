namespace Flare.Application.DTOs;

public record GetFeatureFlagValueDto
{
    public required bool Value { get; init; }
}