using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using Flare.Domain.Constants;
using Flare.Infrastructure.Observability;
using Serilog;
using Serilog.Context;

namespace Flare.Api.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;
    private const string CorrelationIdHeader = "X-Correlation-Id";
    private const string SdkPathPrefix = "/sdk/v1/flags";

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);
        context.Response.Headers[CorrelationIdHeader] = correlationId;

        var stopwatch = Stopwatch.StartNew();
        var requestPath = context.Request.Path.Value ?? "/";
        var httpMethod = context.Request.Method;
        var clientIp = GetClientIp(context);

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("RequestPath", requestPath))
        using (LogContext.PushProperty("HttpMethod", httpMethod))
        using (LogContext.PushProperty("ClientIp", clientIp))
        {
            string? requestBody = null;
            var isSdkEndpoint = requestPath.StartsWith(SdkPathPrefix, StringComparison.OrdinalIgnoreCase);

            if (isSdkEndpoint && context.Request.ContentLength > 0)
            {
                requestBody = await CaptureRequestBodyAsync(context);
            }

            try
            {
                await _next(context);
            }
            finally
            {
                stopwatch.Stop();
                var elapsedMs = stopwatch.ElapsedMilliseconds;

                EnrichWithUserContext(context);

                var statusCode = context.Response.StatusCode;
                var logLevel = GetLogLevel(statusCode);
                var eventName = statusCode >= 500
                    ? LogEventNames.Http.Request.Failed
                    : LogEventNames.Http.Request.Completed;

                _logger.Log(
                    logLevel,
                    "{EventName} {HttpMethod} {RequestPath} responded {StatusCode} in {ElapsedMs}ms",
                    eventName,
                    httpMethod,
                    requestPath,
                    statusCode,
                    elapsedMs);

                if (isSdkEndpoint && !string.IsNullOrEmpty(requestBody))
                {
                    var maskedBody = SensitiveDataMasker.MaskSensitiveFields(requestBody);
                    _logger.LogDebug("SDK request body: {RequestBody}", maskedBody);
                }
            }
        }
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var existingId)
            && !string.IsNullOrWhiteSpace(existingId))
        {
            return existingId.ToString();
        }

        return Guid.NewGuid().ToString("N");
    }

    private static string GetClientIp(HttpContext context)
    {
        // Check for forwarded header first (for load balancers/proxies)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static async Task<string?> CaptureRequestBodyAsync(HttpContext context)
    {
        context.Request.EnableBuffering();

        using var reader = new StreamReader(
            context.Request.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true);

        var body = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;

        return body;
    }

    private static void EnrichWithUserContext(HttpContext context)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(userId))
        {
            LogContext.PushProperty("UserId", userId);
        }

        if (context.Items.TryGetValue(HttpContextKeys.ProjectId, out var projectId) && projectId != null)
        {
            LogContext.PushProperty("ProjectId", projectId);
        }
    }

    private static LogLevel GetLogLevel(int statusCode)
    {
        return statusCode switch
        {
            >= 500 => LogLevel.Error,
            >= 400 => LogLevel.Warning,
            _ => LogLevel.Information
        };
    }
}
