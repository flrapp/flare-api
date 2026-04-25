using System.Text.Json;
using System.Text.Json.Serialization;

namespace Flare.Application.DTOs;

public class FeatureFlagValueDto
{
    public Guid Id { get; set; }
    public Guid ScopeId { get; set; }
    public string ScopeName { get; set; } = string.Empty;
    public string ScopeAlias { get; set; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? BooleanValue { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StringValue { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? NumberValue { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? JsonValue { get; set; }
    public DateTime UpdatedAt { get; set; }
}
