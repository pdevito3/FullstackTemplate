namespace FullstackTemplate.Bff.Sessions;

/// <summary>
/// One BFF session: the row in <c>bffdb</c> that the <c>__Host-bff</c> cookie points to.
/// </summary>
/// <param name="Key">A random 256-bit opaque id. The cookie holds it through Data Protection.</param>
/// <param name="SubjectId">The <c>sub</c> claim.</param>
/// <param name="SessionId">The <c>sid</c> claim, or null if the IdP session has none.</param>
/// <param name="Created">When the login created the BFF session.</param>
/// <param name="Renewed">When the last renewal wrote the row.</param>
/// <param name="Expires">The sliding expiry. It never passes <paramref name="AbsoluteExpires"/>.</param>
/// <param name="AbsoluteExpires"><paramref name="Created"/> plus the absolute limit. It never moves.</param>
/// <param name="Ticket">The serialized authentication ticket with the tokens, encrypted with Data Protection.</param>
/// <param name="Version">The concurrency token of the row. On Postgres, this is <c>xmin</c>.</param>
public sealed record BffSession(
    string Key,
    string SubjectId,
    string? SessionId,
    DateTimeOffset Created,
    DateTimeOffset Renewed,
    DateTimeOffset Expires,
    DateTimeOffset AbsoluteExpires,
    byte[] Ticket,
    uint Version)
{
    /// <summary>
    /// The earlier of the sliding expiry and the absolute limit.
    /// </summary>
    public DateTimeOffset EndsAt => Expires < AbsoluteExpires ? Expires : AbsoluteExpires;

    /// <summary>
    /// True when the sliding expiry or the absolute limit is not after <paramref name="now"/>.
    /// </summary>
    public bool HasEnded(DateTimeOffset now) => EndsAt <= now;
}
