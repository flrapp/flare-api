using Microsoft.Extensions.Logging;

namespace Flare.UnitTests.TestSupport;

internal sealed record CapturedLog(string Category, LogLevel Level, string Message, IReadOnlyDictionary<string, object?> Properties);

/// Captures structured log entries so tests can assert on category and template properties.
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    public List<CapturedLog> Entries { get; } = [];

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, Entries);

    public void Dispose() { }

    private sealed class CapturingLogger(string category, List<CapturedLog> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var properties = state is IEnumerable<KeyValuePair<string, object?>> pairs
                ? pairs.ToDictionary(p => p.Key, p => p.Value)
                : new Dictionary<string, object?>();
            entries.Add(new CapturedLog(category, logLevel, formatter(state, exception), properties));
        }
    }
}
