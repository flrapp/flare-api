using System.Text.Json;
using Flare.Domain.Exceptions;

namespace Flare.Domain.Entities;

public class TargetingRule
{
    public Guid Id { get; set; }
    public Guid FeatureFlagValueId { get; set; }
    public FeatureFlagValue FeatureFlagValue { get; set; } = null!;
    public int Priority { get; set; }

    public bool? ServeBooleanValue { get; private set; }
    public string? ServeStringValue { get; private set; }
    public double? ServeNumberValue { get; private set; }
    public string? ServeJsonValue { get; private set; }

    public ICollection<TargetingCondition> Conditions { get; set; } = new List<TargetingCondition>();

    private TargetingRule() { }

    public static TargetingRule ForBoolean(
        Guid flagValueId, int priority, bool value,
        IEnumerable<TargetingCondition>? conditions = null)
    {
        var rule = New(flagValueId, priority, conditions);
        rule.ServeBooleanValue = value;
        return rule;
    }

    public static TargetingRule ForString(
        Guid flagValueId, int priority, string value,
        IEnumerable<TargetingCondition>? conditions = null)
    {
        var rule = New(flagValueId, priority, conditions);
        rule.ServeStringValue = Require(value, "string");
        return rule;
    }

    public static TargetingRule ForNumber(
        Guid flagValueId, int priority, double value,
        IEnumerable<TargetingCondition>? conditions = null)
    {
        var rule = New(flagValueId, priority, conditions);
        rule.ServeNumberValue = value;
        return rule;
    }

    public static TargetingRule ForJson(
        Guid flagValueId, int priority, string value,
        IEnumerable<TargetingCondition>? conditions = null)
    {
        var rule = New(flagValueId, priority, conditions);
        rule.ServeJsonValue = RequireJson(value);
        return rule;
    }

    public void SetBoolean(bool value)
    {
        ClearServeColumns();
        ServeBooleanValue = value;
    }

    public void SetString(string value)
    {
        ClearServeColumns();
        ServeStringValue = Require(value, "string");
    }

    public void SetNumber(double value)
    {
        ClearServeColumns();
        ServeNumberValue = value;
    }

    public void SetJson(string value)
    {
        ClearServeColumns();
        ServeJsonValue = RequireJson(value);
    }

    private void ClearServeColumns()
    {
        ServeBooleanValue = null;
        ServeStringValue = null;
        ServeNumberValue = null;
        ServeJsonValue = null;
    }

    private static TargetingRule New(
        Guid flagValueId, int priority, IEnumerable<TargetingCondition>? conditions) =>
        new()
        {
            Id = Guid.NewGuid(),
            FeatureFlagValueId = flagValueId,
            Priority = priority,
            Conditions = conditions?.ToList() ?? new List<TargetingCondition>()
        };

    private static string Require(string value, string field)
    {
        if (value is null)
            throw new ValidationException(new Dictionary<string, string[]>
            {
                [$"serveValue.{field}"] = new[] { $"{field} value is required" }
            });
        return value;
    }

    private static string RequireJson(string value)
    {
        Require(value, "json");
        try
        {
            using var _ = JsonDocument.Parse(value);
        }
        catch (JsonException ex)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["serveValue.json"] = new[] { $"json value is not valid JSON: {ex.Message}" }
            });
        }
        return value;
    }
}
