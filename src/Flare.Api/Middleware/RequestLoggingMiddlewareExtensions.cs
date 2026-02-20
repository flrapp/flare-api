namespace Flare.Api.Middleware;

/// <summary>
/// Extension methods for registering the RequestLoggingMiddleware.
/// </summary>
public static class RequestLoggingMiddlewareExtensions
{
    /// <summary>
    /// Adds structured request logging middleware to the pipeline.
    /// </summary>
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestLoggingMiddleware>();
    }
}
