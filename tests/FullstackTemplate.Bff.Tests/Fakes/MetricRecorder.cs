namespace FullstackTemplate.Bff.Tests.Fakes;

using System.Diagnostics.Metrics;

/// <summary>
/// Records the measurements of the <c>FullstackTemplate.Bff</c> meter from its creation until it is disposed.
/// </summary>
/// <remarks>
/// A <see cref="MeterListener"/> sees the measurements of the whole process. A test that uses it must not run at
/// the same time as other tests, so mark it with <c>[NotInParallel]</c> and no key.
/// </remarks>
public sealed class MetricRecorder : IDisposable
{
    private readonly MeterListener _listener = new();
    private readonly List<(string Name, long Value, Dictionary<string, object?> Tags)> _measurements = [];

    public MetricRecorder()
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == Telemetry.ServiceName)
                listener.EnableMeasurementEvents(instrument);
        };
        _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            var tagMap = new Dictionary<string, object?>();
            foreach (var tag in tags)
                tagMap[tag.Key] = tag.Value;
            lock (_measurements)
                _measurements.Add((instrument.Name, value, tagMap));
        });
        _listener.Start();
    }

    /// <summary>
    /// The sum of the measurements of the counter <paramref name="name"/>, only of those with the tag
    /// <paramref name="tag"/> when it is given.
    /// </summary>
    public long Sum(string name, (string Key, string Value)? tag = null)
    {
        lock (_measurements)
        {
            return _measurements
                .Where(m => m.Name == name)
                .Where(m => tag is not { } t || (m.Tags.TryGetValue(t.Key, out var value) && Equals(value, t.Value)))
                .Sum(m => m.Value);
        }
    }

    /// <summary>
    /// The tag sets of the measurements of the counter <paramref name="name"/>.
    /// </summary>
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> TagsOf(string name)
    {
        lock (_measurements)
            return _measurements.Where(m => m.Name == name).Select(m => (IReadOnlyDictionary<string, object?>)m.Tags).ToList();
    }

    public void Dispose() => _listener.Dispose();
}
