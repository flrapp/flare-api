using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Flare.Domain.Entities;
using Flare.Domain.Enums;
using ValidationException = Flare.Domain.Exceptions.ValidationException;

namespace Flare.Application.DTOs;

public class UpdateFeatureFlagValueDto
{
    [Required]
    public Guid ScopeId { get; set; }

    public bool? BooleanValue { get; set; }
    public double? NumberValue { get; set; }
    public string? StringValue { get; set; }
    public JsonElement? JsonValue { get; set; }

    public FeatureFlagType Type { get; set; }

    public void ApplyDefault(FeatureFlagValue value)
    {
        switch (Type)
        {
            case FeatureFlagType.Boolean:
                value.SetBooleanDefault(BooleanValue ?? false);
                break;
            case FeatureFlagType.String:
                value.SetStringDefault(StringValue);
                break;
            case FeatureFlagType.Number:
                value.SetNumberDefault(NumberValue);
                break;
            case FeatureFlagType.Json:
                value.SetJsonDefault(JsonValue?.GetRawText());
                break;
            default:
                throw new ValidationException(new Dictionary<string, string[]>
                {
                    ["type"] = new[] { $"Unsupported flag type '{Type}'." }
                });
        }
    }
}
