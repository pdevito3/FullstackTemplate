using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace FullstackTemplate.Bff.Sessions;

/// <summary>
/// The lock of an IdP session in <c>bffdb</c>: <c>pg_advisory_xact_lock</c> on a hash of its <c>sub</c> or of its
/// <c>sid</c>. It holds until the transaction ends.
/// <para>
/// A login and a back-channel logout of one IdP session must not run at the same time. Each of them reads what the
/// other writes: the login reads the logout records, and the logout reads the rows. Under READ COMMITTED each read
/// misses an uncommitted write of the other, so with no lock both can commit, and the login leaves a live row after
/// its logout.
/// </para>
/// <para>
/// The two-key form of the lock has a key space of its own, so it never waits for the lock of a refresh
/// (<c>RefreshLock</c>). The transaction also gets an <c>idle_in_transaction_session_timeout</c>, so that Postgres
/// ends the session, and frees the lock, of an instance that stops with its connection open.
/// </para>
/// </summary>
public static class IdpSessionLock
{
    private const int SubjectKey = 1;
    private const int SessionIdKey = 2;

    private static readonly TimeSpan IdleInTransactionLimit = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Takes the lock of the <c>sub</c>, and then of the <c>sid</c>, of a login. A logout token can name either of
    /// them.
    /// </summary>
    public static async Task TakeForLoginAsync(BffDbContext dbContext, string subjectId, string? sessionId,
        CancellationToken cancellationToken)
    {
        await TakeAsync(dbContext, SubjectKey, subjectId, cancellationToken);
        if (sessionId is not null)
            await TakeAsync(dbContext, SessionIdKey, sessionId, cancellationToken);
    }

    /// <summary>
    /// Takes the lock of the <c>sid</c> of a logout token, or of its <c>sub</c> when it has no <c>sid</c>: the
    /// lock of each login that the token ends.
    /// </summary>
    public static Task TakeForLogoutAsync(BffDbContext dbContext, string? subjectId, string? sessionId,
        CancellationToken cancellationToken) =>
        sessionId is not null
            ? TakeAsync(dbContext, SessionIdKey, sessionId, cancellationToken)
            : TakeAsync(dbContext, SubjectKey, subjectId!, cancellationToken);

    private static Task<int> TakeAsync(BffDbContext dbContext, int kind, string value, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            -- bff_idp_session.lock
            SELECT set_config('idle_in_transaction_session_timeout', {$"{(long)IdleInTransactionLimit.TotalMilliseconds}ms"}, true),
                pg_advisory_xact_lock({kind}, {BinaryPrimitives.ReadInt32BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes(value)))})
            """,
            cancellationToken);
}
