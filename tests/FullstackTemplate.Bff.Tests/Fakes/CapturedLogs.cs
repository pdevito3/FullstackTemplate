namespace FullstackTemplate.Bff.Tests.Fakes;

using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;

/// <summary>
/// A Serilog sink that keeps each log event of the BFF in memory, so that a test can read what the BFF logged.
/// </summary>
public sealed class CapturedLogs : ILogEventSink
{
    private readonly ConcurrentQueue<LogEvent> _events = new();

    public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);

    /// <summary>
    /// The events that have a property whose value contains <paramref name="text"/>.
    /// </summary>
    public IReadOnlyList<LogEvent> WithPropertyContaining(string text) =>
        _events.Where(e => e.Properties.Values.Any(v => v.ToString().Contains(text, StringComparison.Ordinal))).ToList();

    public IReadOnlyList<LogEvent> AtLeast(LogEventLevel level) => _events.Where(e => e.Level >= level).ToList();
}
