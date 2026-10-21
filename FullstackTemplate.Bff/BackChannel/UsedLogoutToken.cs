namespace FullstackTemplate.Bff.BackChannel;

/// <summary>
/// One row of the replay cache of the logout tokens in <c>bffdb</c>: the <c>jti</c> of a logout token that the BFF
/// accepted. A second token with the same <c>jti</c> is a replay.
/// </summary>
/// <param name="Jti">The <c>jti</c> claim of the logout token.</param>
/// <param name="Expires">The <c>exp</c> of the logout token, plus the clock skew of the validation. After this time
/// the token cannot pass the validation again, so the cleanup can delete the row.</param>
public sealed record UsedLogoutToken(string Jti, DateTimeOffset Expires);
