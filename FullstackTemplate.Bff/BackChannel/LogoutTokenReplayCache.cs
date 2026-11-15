using FullstackTemplate.Bff.Sessions;
using Microsoft.EntityFrameworkCore;

namespace FullstackTemplate.Bff.BackChannel;

/// <summary>
/// The replay cache of the logout tokens: a table in <c>bffdb</c>, keyed by <c>jti</c>, so that all BFF instances
/// share it. The cleanup deletes a row after the token expires.
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
    /// Adds <paramref name="jti"/>. Returns false, and changes nothing, when the row exists, for example because
    /// another request with the same token added it first.
    /// </summary>
    public async Task<bool> TryAddAsync(string jti, DateTimeOffset expires, CancellationToken cancellationToken) =>
        // EF Core has no insert that ignores a duplicate key, and a failed SaveChanges logs an error
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            -- bff_used_logout_tokens.add
            INSERT INTO used_logout_tokens (jti, expires) VALUES ({jti}, {expires.ToUniversalTime()})
            ON CONFLICT (jti) DO NOTHING
            """,
            cancellationToken) > 0;

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
