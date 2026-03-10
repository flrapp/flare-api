using System.Threading.RateLimiting;
using Flare.Domain.Constants;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Flare.Api.RateLimiting;

internal sealed class SdkEvaluationRateLimiterPolicy : IRateLimiterPolicy<string>
{
    private readonly PartitionedRateLimiter<string> _chainedLimiter;

    public SdkEvaluationRateLimiterPolicy(IOptions<RateLimitingOptions> options)
    {
        var opts = options.Value;

        var globalLimiter = PartitionedRateLimiter.Create<string, string>(_ =>
            RateLimitPartition.GetSlidingWindowLimiter("global", __ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = opts.Global.PermitsPerSecond,
                Window = TimeSpan.FromSeconds(opts.Global.WindowSeconds),
                SegmentsPerWindow = 10,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

        var perProjectLimiter = PartitionedRateLimiter.Create<string, string>(apiKey =>
            RateLimitPartition.GetSlidingWindowLimiter(apiKey, _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = opts.PerProject.PermitsPerSecond,
                Window = TimeSpan.FromSeconds(opts.PerProject.WindowSeconds),
                SegmentsPerWindow = 10,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

        _chainedLimiter = PartitionedRateLimiter.CreateChained(globalLimiter, perProjectLimiter);
    }

    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        var apiKey = ExtractApiKey(httpContext) ?? "_anonymous_";
        return RateLimitPartition.Get(apiKey, key => new ChainedRateLimiterAdapter(_chainedLimiter, key));
    }

    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => null;

    private static string? ExtractApiKey(HttpContext context)
    {
        var apiKey = context.Request.Headers[HeadersKeys.ApiKeyHeaderName].FirstOrDefault();
        return string.IsNullOrEmpty(apiKey) ? null : apiKey;
    }
}

internal sealed class ChainedRateLimiterAdapter : RateLimiter
{
    private readonly PartitionedRateLimiter<string> _inner;
    private readonly string _partitionKey;

    internal ChainedRateLimiterAdapter(PartitionedRateLimiter<string> inner, string partitionKey)
    {
        _inner = inner;
        _partitionKey = partitionKey;
    }

    public override TimeSpan? IdleDuration => null;

    protected override RateLimitLease AttemptAcquireCore(int permitCount)
        => _inner.AttemptAcquire(_partitionKey, permitCount);

    protected override ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
        => _inner.AcquireAsync(_partitionKey, permitCount, cancellationToken);

    public override RateLimiterStatistics? GetStatistics()
        => _inner.GetStatistics(_partitionKey);

    protected override void Dispose(bool disposing) { }
}
