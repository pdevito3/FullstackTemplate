namespace FullstackTemplate.FunctionalTests.TestUtilities;

using System.Collections.Concurrent;
using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

public sealed class CapturedLogs : ILogEventSink
{
    private readonly ConcurrentQueue<LogEvent> _events = new();

    public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);

    public IReadOnlyList<LogEvent> InTrace(ActivityTraceId traceId) =>
        _events.Where(e => e.TraceId == traceId).ToList();

    public IReadOnlyList<LogEvent> WithUserAgent(string userAgent) =>
        _events.Where(e => e.Properties.TryGetValue("UserAgent", out var value)
            && value is ScalarValue { Value: string requestUserAgent } && requestUserAgent == userAgent).ToList();
}
