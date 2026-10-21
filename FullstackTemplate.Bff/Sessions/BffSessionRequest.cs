using Microsoft.AspNetCore.Authentication;

namespace FullstackTemplate.Bff.Sessions;

/// <summary>
/// What one request knows about its BFF session: the key, the version of the row that it read, and the tokens
/// in that row. A conditional write uses the version, and the version moves forward after each write that
/// succeeds. Scoped, so each request has its own.
/// </summary>
public sealed class BffSessionRequest
{
    public string? Key { get; private set; }

    public uint Version { get; private set; }

    public DateTimeOffset AbsoluteExpires { get; private set; }

    public SessionTokens Tokens { get; private set; } = SessionTokens.None;

    public void Remember(BffSession session, SessionTokens tokens)
    {
        Key = session.Key;
        Version = session.Version;
        AbsoluteExpires = session.AbsoluteExpires;
        Tokens = tokens;
    }

    public void Written(uint version, SessionTokens tokens)
    {
        Version = version;
        Tokens = tokens;
    }

    public void Written(uint version) => Version = version;

    public void Forget()
    {
        Key = null;
        Version = 0;
        Tokens = SessionTokens.None;
    }
}

/// <summary>
/// The tokens of a ticket. Two snapshots are equal when no token changed.
/// </summary>
public sealed record SessionTokens(string? AccessToken, string? RefreshToken, string? ExpiresAt, string? IdToken)
{
    public static readonly SessionTokens None = new(null, null, null, null);

    public static SessionTokens From(AuthenticationProperties properties) => new(
        properties.GetTokenValue("access_token"),
        properties.GetTokenValue("refresh_token"),
        properties.GetTokenValue("expires_at"),
        properties.GetTokenValue("id_token"));
}
