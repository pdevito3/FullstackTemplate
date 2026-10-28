namespace FullstackTemplate.Server.Resources.Entrypoints.DPoP;

/// <summary>
/// The "Auth:DPoP" configuration section: the limits of the DPoP proof check (RFC 9449 section 4.3).
/// </summary>
public sealed class DPoPOptions
{
    public const string SectionName = "Auth:DPoP";

    /// <summary>
    /// The oldest <c>iat</c> that a proof can have. A proof is made for one request, so the window is short.
    /// </summary>
    public TimeSpan ProofLifetime { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// How far in the future an <c>iat</c> can be, for the difference between the clocks of the client and the server.
    /// </summary>
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The interval at which the cleanup deletes the expired rows of the replay cache.
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The time for which the replay cache keeps a <c>jti</c>: the longest time that a proof can still pass the
    /// <c>iat</c> check.
    /// </summary>
    public TimeSpan ReplayWindow => ProofLifetime + ClockSkew;
}
