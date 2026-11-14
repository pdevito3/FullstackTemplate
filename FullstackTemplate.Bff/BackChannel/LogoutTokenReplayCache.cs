using FullstackTemplate.Bff.Sessions;
using Microsoft.EntityFrameworkCore;

namespace FullstackTemplate.Bff.BackChannel;

/// <summary>
/// The replay cache of the logout tokens: a table in <c>bffdb</c>, keyed by <c>jti</c>, so that all BFF instances
/// share it. The cleanup deletes a row after it expires.
/// </summary>
public sealed class LogoutTokenReplayCache(BffDbContext dbContext)
{
    /// <summary>
    /// True when a logout token with <paramref name="jti"/> was accepted before.
    /// </summary>
    public Task<bool> IsUsedAsync(string jti, CancellationToken cancellationToken) =>
        dbContext.UsedLogoutTokens.AsNoTracking()
            .TagWith("bff_used_logout_tokens.is_used")
            .AnyAsync(t => t.Jti == jti, cancellationToken);

    /// <summary>
    /// Adds <paramref name="token"/>. Returns false, and changes nothing, when a row with its <c>jti</c> exists, for
    /// example because another request with the same token added it first.
    /// </summary>
    public async Task<bool> TryAddAsync(UsedLogoutToken token, CancellationToken cancellationToken) =>
        // EF Core has no insert that ignores a duplicate key, and a failed SaveChanges logs an error
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            -- bff_used_logout_tokens.add
            INSERT INTO used_logout_tokens (jti, session_id, subject, issued_at, expires)
            VALUES ({token.Jti}, {token.SessionId}, {token.Subject}, {token.IssuedAt.ToUniversalTime()}, {token.Expires.ToUniversalTime()})
            ON CONFLICT (jti) DO NOTHING
            """,
            cancellationToken) > 0;

    /// <summary>
    /// True when an accepted logout token ends a login of <paramref name="subjectId"/> and
    /// <paramref name="sessionId"/>: a token of the <c>sid</c>, or a token with no <c>sid</c> of the <c>sub</c>,
    /// issued at or after <paramref name="issuedFrom"/> (<see cref="LoginTimes.EndedByLogoutsFrom"/>).
    /// </summary>
    public Task<bool> EndsLoginAsync(string subjectId, string? sessionId, DateTimeOffset issuedFrom,
        CancellationToken cancellationToken) =>
        dbContext.UsedLogoutTokens.AsNoTracking()
            .TagWith("bff_used_logout_tokens.ends_login")
            .AnyAsync(t => ((sessionId != null && t.SessionId == sessionId) || (t.SessionId == null && t.Subject == subjectId))
                && t.IssuedAt >= issuedFrom, cancellationToken);

    /// <summary>
    /// Deletes at most <paramref name="batchSize"/> rows that expired at or before <paramref name="now"/>, and
    /// returns the number of rows that it deleted.
    /// </summary>
    public Task<int> DeleteExpiredAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken)
    {
        var batch = dbContext.UsedLogoutTokens
            .Where(t => t.Expires <= now)
            .OrderBy(t => t.Jti)
            .Take(batchSize)
            .Select(t => t.Jti);

        return dbContext.UsedLogoutTokens
            .TagWith("bff_used_logout_tokens.delete_expired")
            .Where(t => batch.Contains(t.Jti))
            .ExecuteDeleteAsync(cancellationToken);
    }
}
