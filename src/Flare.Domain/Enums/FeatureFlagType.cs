using System.Text.Json.Serialization;

namespace Flare.Domain.Enums;

public enum FeatureFlagType
{
    [JsonStringEnumMemberName("boolean")]
    Boolean = 0,
    [JsonStringEnumMemberName("string")]
    String = 1,
    [JsonStringEnumMemberName("number")]
    Number = 2,
    [JsonStringEnumMemberName("json")]
    Json = 3
}
