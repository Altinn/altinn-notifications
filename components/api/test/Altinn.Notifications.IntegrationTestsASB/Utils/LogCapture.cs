using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

namespace Altinn.Notifications.IntegrationTestsASB.Utils;

/// <summary>
/// A simple logger provider that captures log entries containing a specific pattern.
/// Used in integration tests to count handler invocations via log output.
/// </summary>
public sealed class LogCapture : ILoggerProvider
{
    private readonly string _pattern;
    private readonly ConcurrentBag<string> _entries = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="LogCapture"/> class.
    /// </summary>
    /// <param name="pattern">The substring pattern to match in log messages or exception types.</param>
    public LogCapture(string pattern) => _pattern = pattern;

    /// <summary>
    /// Gets the number of log entries that matched the pattern.
    /// </summary>
    public int Count => _entries.Count;

    /// <inheritdoc/>
    public ILogger CreateLogger(string categoryName) => new CaptureLogger(this);

    /// <inheritdoc/>
    public void Dispose()
    {
    }

    private sealed class CaptureLogger(LogCapture owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            string message = formatter(state, exception);
            string? exceptionType = exception?.GetType().Name;

            if (message.Contains(owner._pattern, StringComparison.OrdinalIgnoreCase)
                || (exceptionType != null && exceptionType.Contains(owner._pattern, StringComparison.OrdinalIgnoreCase)))
            {
                owner._entries.Add(message);
            }
        }
    }
}
