using Microsoft.Extensions.Logging;

namespace NotificationForwarder.IntegrationTests.Utilities;

/// <summary>Routes the host's log lines into the test output so a failing test shows what the worker did.</summary>
internal sealed class XUnitLoggerProvider(ITestOutputHelper output) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new XUnitLogger(output, categoryName);

    public void Dispose()
    {
    }

    private sealed class XUnitLogger(ITestOutputHelper output, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            try
            {
                output.WriteLine($"{logLevel} {category}: {formatter(state, exception)}{(exception is null ? string.Empty : " | " + exception.Message)}");
            }
            catch (InvalidOperationException)
            {
                // The test has already finished; the host is still shutting down.
            }
        }
    }
}
