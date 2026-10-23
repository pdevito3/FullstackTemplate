namespace FullstackTemplate.IntegrationTests.DomainEvents;

using System.Collections.Concurrent;
using System.Diagnostics;
using FullstackTemplate.Server.Resources;

public sealed class SpanRecorder : IDisposable
{
    private readonly ActivityListener _listener;
    private readonly ConcurrentQueue<Activity> _spans = new();
    private readonly Activity _root;

    public SpanRecorder()
    {
        _root = new Activity("test.root").Start();
        var traceId = _root.TraceId;
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == Telemetry.ServiceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => options.TraceId == traceId
                ? ActivitySamplingResult.AllDataAndRecorded
                : ActivitySamplingResult.None,
            ActivityStopped = activity =>
            {
                if (activity.TraceId == traceId)
                    _spans.Enqueue(activity);
            },
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public IReadOnlyList<Activity> Spans => [.. _spans];

    public void Dispose()
    {
        _root.Stop();
        _listener.Dispose();
    }
}
