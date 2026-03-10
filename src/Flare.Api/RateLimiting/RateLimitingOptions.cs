namespace Flare.Api.RateLimiting;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";
    public const string SdkEvaluationPolicyName = "sdk-evaluation";

    public GlobalRateLimitOptions Global { get; init; } = new();
    public PerProjectRateLimitOptions PerProject { get; init; } = new();
}

public sealed class GlobalRateLimitOptions
{
    public int PermitsPerSecond { get; init; } = 100;
    public int WindowSeconds { get; init; } = 1;
}

public sealed class PerProjectRateLimitOptions
{
    public int PermitsPerSecond { get; init; } = 20;
    public int WindowSeconds { get; init; } = 1;
}
