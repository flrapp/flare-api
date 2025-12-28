namespace Flare.Application.DTOs;

public class RegenerateApiKeyResponseDto
{
    public string ApiKey { get; set; } = string.Empty;
    public DateTime RegeneratedAt { get; set; }
}
