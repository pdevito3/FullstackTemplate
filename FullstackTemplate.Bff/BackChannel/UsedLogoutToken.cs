using FullstackTemplate.Bff.Sessions;

namespace FullstackTemplate.Bff.BackChannel;

/// <summary>
/// One row of the replay cache of the logout tokens in <c>bffdb</c>: a logout token that the BFF accepted. A second
/// token with the same <c>jti</c> is a replay. A login of the IdP session of the token that writes its row after the
/// token came fails (<see cref="LogoutTokenReplayCache.EndsLoginAsync"/>).
/// </summary>
/// <param name="Jti">The <c>jti</c> claim of the logout token.</param>
/// <param name="SessionId">The <c>sid</c> claim. The token ends the logins of this <c>sid</c>.</param>
/// <param name="Subject">The <c>sub</c> claim. A token with no <c>sid</c> ends all the logins of this <c>sub</c>.</param>
/// <param name="IssuedAt">The <c>iat</c> claim. The token ends only the logins whose tokens came before it
/// (<see cref="LoginTimes.EndedByLogoutsFrom"/>).</param>
/// <param name="Expires">The time after which the cleanup can delete the row. See <see cref="Of"/>.</param>
public sealed record UsedLogoutToken(string Jti, string? SessionId, string? Subject, DateTimeOffset IssuedAt, DateTimeOffset Expires)
{
    /// <summary>
    /// The row of <paramref name="token"/>, received at <paramref name="received"/>. It expires at the later of two
    /// times. Until the first, the token can pass the checks again. Until the second, a login of the IdP session that
    /// began before the token came can still write its row.
    /// </summary>
    public static UsedLogoutToken Of(LogoutToken token, DateTimeOffset received, TimeSpan remoteAuthenticationTimeout)
    {
        var lastDeadline = LoginTimes.LatestDeadlineBefore(received, remoteAuthenticationTimeout);
        return new UsedLogoutToken(token.Jti, token.SessionId, token.Subject, token.IssuedAt,
            token.Expires > lastDeadline ? token.Expires : lastDeadline);
    }
}
