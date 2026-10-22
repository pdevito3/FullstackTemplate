namespace FullstackTemplate.Bff.Sessions;

/// <summary>
/// The store of the BFF sessions. The EF Core adapter on Postgres is for production. The tests also have an
/// in-memory adapter. Each write that changes a row is conditional on the <see cref="BffSession.Version"/> that
/// the caller read, so that a late write cannot replace newer data or bring a deleted BFF session back.
/// </summary>
public interface IBffSessionStore
{
    /// <summary>
    /// Adds a new row and returns it with its version.
    /// </summary>
    Task<BffSession> CreateAsync(BffSession session, CancellationToken cancellationToken);

    /// <summary>
    /// The row of <paramref name="key"/>, or null if there is none or if it has ended.
    /// </summary>
    Task<BffSession?> GetAsync(string key, CancellationToken cancellationToken);

    /// <summary>
    /// Writes a new ticket and expiry, only if the row still has <paramref name="expectedVersion"/>.
    /// It never creates a row.
    /// </summary>
    Task<BffSessionWrite> RenewAsync(string key, byte[] ticket, DateTimeOffset renewed, DateTimeOffset expires,
        uint expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// Writes only a new expiry, only if the row still has <paramref name="expectedVersion"/>. The ticket, and
    /// thus the tokens, stay as they are. It never creates a row.
    /// </summary>
    Task<BffSessionWrite> RenewExpiryAsync(string key, DateTimeOffset renewed, DateTimeOffset expires,
        uint expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the row of <paramref name="key"/> and returns it as it was at the delete, or null if there was none.
    /// The caller can then revoke the refresh token of the row, also when another request wrote new tokens after
    /// the caller read the row.
    /// </summary>
    Task<BffSession?> DeleteAsync(string key, CancellationToken cancellationToken);

    /// <summary>
    /// The rows of the <c>sub</c> <paramref name="subjectId"/> that have not ended.
    /// </summary>
    Task<IReadOnlyList<BffSession>> FindBySubjectAsync(string subjectId, CancellationToken cancellationToken);

    /// <summary>
    /// The rows of the <c>sid</c> <paramref name="sessionId"/> that have not ended.
    /// </summary>
    Task<IReadOnlyList<BffSession>> FindBySessionIdAsync(string sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes at most <paramref name="batchSize"/> rows that ended at or before <paramref name="now"/>, and
    /// returns the number of rows that it deleted.
    /// </summary>
    Task<int> DeleteEndedAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken);
}

/// <summary>
/// The result of a conditional write.
/// </summary>
public abstract record BffSessionWrite
{
    private BffSessionWrite()
    {
    }

    /// <summary>
    /// The write is in the row. <paramref name="Version"/> is the new version of the row.
    /// </summary>
    public sealed record Written(uint Version) : BffSessionWrite;

    /// <summary>
    /// The row is gone. The store did not create it again.
    /// </summary>
    public sealed record Missing : BffSessionWrite
    {
        public static readonly Missing Instance = new();
    }

    /// <summary>
    /// Another write changed the row after the read. <paramref name="Current"/> is the row as it is now.
    /// </summary>
    public sealed record Conflict(BffSession Current) : BffSessionWrite;
}
