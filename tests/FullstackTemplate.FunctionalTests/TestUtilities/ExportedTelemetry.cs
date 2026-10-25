namespace FullstackTemplate.FunctionalTests.TestUtilities;

using System.Collections;
using System.Collections.Concurrent;

/// <summary>
/// The spans or metrics that an OpenTelemetry in-memory exporter collects. The exporter adds each item on the
/// thread that exports it and takes no lock, so a read of a plain list can see it half grown. Tests read a
/// <see cref="Snapshot"/> instead.
/// </summary>
public sealed class ExportedTelemetry<T>
{
    private readonly ConcurrentQueue<T> _items = new();

    public ExportedTelemetry() => ExporterTarget = new AddOnlyCollection(_items);

    /// <summary>
    /// The collection to give to <c>AddInMemoryExporter</c>. It takes adds only.
    /// </summary>
    public ICollection<T> ExporterTarget { get; }

    public int Count => _items.Count;

    /// <summary>
    /// A copy of the items in export order. The exporter can add more while the caller reads the copy.
    /// </summary>
    public IReadOnlyList<T> Snapshot() => _items.ToArray();

    private sealed class AddOnlyCollection(ConcurrentQueue<T> items) : ICollection<T>
    {
        public int Count => items.Count;

        public bool IsReadOnly => false;

        public void Add(T item) => items.Enqueue(item);

        public void Clear() => throw new NotSupportedException();

        public bool Contains(T item) => throw new NotSupportedException();

        public void CopyTo(T[] array, int arrayIndex) => throw new NotSupportedException();

        public bool Remove(T item) => throw new NotSupportedException();

        public IEnumerator<T> GetEnumerator() => throw new NotSupportedException();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
