namespace FullstackTemplate.Bff.Tests.Fakes;

using System.Collections.Concurrent;
using FullstackTemplate.Bff.Sessions;

/// <summary>
/// The in-memory adapter of <see cref="IBffSessionStore"/>, for tests only. Each write gives the row a new
/// version, as <c>xmin</c> does on Postgres, and a write with an old version changes nothing.
/// </summary>
public sealed class InMemoryBffSessionStore(TimeProvider timeProvider) : IBffSessionStore
{
    private readonly ConcurrentDictionary<string, BffSession> _rows = new(StringComparer.Ordinal);
    private readonly Lock _writeLock = new();
    private uint _lastVersion;

    public Task<BffSession> CreateAsync(BffSession session, CancellationToken cancellationToken)
    {
        lock (_writeLock)
        {
            var created = session with { Version = NextVersion() };
            if (!_rows.TryAdd(created.Key, created))
                throw new InvalidOperationException("A BFF session with this key exists.");
            return Task.FromResult(created);
        }
    }

    public Task<BffSession?> GetAsync(string key, CancellationToken cancellationToken) =>
        Task.FromResult(Live(key));

    public Task<BffSessionWrite> RenewAsync(string key, byte[] ticket, DateTimeOffset renewed, DateTimeOffset expires,
        uint expectedVersion, CancellationToken cancellationToken) =>
        Task.FromResult(Write(key, expectedVersion, s => s with { Ticket = ticket, Renewed = renewed, Expires = expires }));

    public Task<BffSessionWrite> RenewExpiryAsync(string key, DateTimeOffset renewed, DateTimeOffset expires,
        uint expectedVersion, CancellationToken cancellationToken) =>
        Task.FromResult(Write(key, expectedVersion, s => s with { Renewed = renewed, Expires = expires }));

    public Task<bool> DeleteAsync(string key, CancellationToken cancellationToken)
    {
        lock (_writeLock)
            return Task.FromResult(_rows.TryRemove(key, out _));
    }

    public Task<IReadOnlyList<BffSession>> FindBySubjectAsync(string subjectId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BffSession>>(LiveRows().Where(s => s.SubjectId == subjectId).ToList());

    public Task<IReadOnlyList<BffSession>> FindBySessionIdAsync(string sessionId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BffSession>>(LiveRows().Where(s => s.SessionId == sessionId).ToList());

    public Task<int> DeleteEndedAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken)
    {
        lock (_writeLock)
        {
            var ended = _rows.Values.Where(s => s.HasEnded(now)).OrderBy(s => s.Key, StringComparer.Ordinal).Take(batchSize).ToList();
            foreach (var session in ended)
                _rows.TryRemove(session.Key, out _);
            return Task.FromResult(ended.Count);
        }
    }

    private BffSessionWrite Write(string key, uint expectedVersion, Func<BffSession, BffSession> change)
    {
        lock (_writeLock)
        {
            if (!_rows.TryGetValue(key, out var current))
                return BffSessionWrite.Missing.Instance;
            if (current.Version != expectedVersion)
                return Live(key) is { } live ? new BffSessionWrite.Conflict(live) : BffSessionWrite.Missing.Instance;

            var written = change(current) with { Version = NextVersion() };
            _rows[key] = written;
            return new BffSessionWrite.Written(written.Version);
        }
    }

    private BffSession? Live(string key) =>
        _rows.TryGetValue(key, out var session) && !session.HasEnded(timeProvider.GetUtcNow()) ? session : null;

    private IEnumerable<BffSession> LiveRows()
    {
        var now = timeProvider.GetUtcNow();
        return _rows.Values.Where(s => !s.HasEnded(now));
    }

    private uint NextVersion() => ++_lastVersion;
}
