namespace FullstackTemplate.Bff.Tests.Fakes;

/// <summary>
/// The optional features of a <see cref="FakeIdentityProvider"/>, as its discovery document lists them. The defaults
/// list every feature except PAR, as Keycloak does with PAR off.
/// </summary>
public sealed record FakeProviderOptions
{
    /// <summary>
    /// Lists <c>authorization_response_iss_parameter_supported</c> and sends <c>iss</c> on each callback.
    /// </summary>
    public bool IssuerParameter { get; init; } = true;

    public bool EndSession { get; init; } = true;

    public bool Revocation { get; init; } = true;

    public bool BackChannelLogout { get; init; } = true;

    public bool PushedAuthorization { get; init; }

    /// <summary>
    /// Lists <c>dpop_signing_alg_values_supported</c> (ES256) and binds tokens to the DPoP key of a login (RFC 9449):
    /// the token endpoint checks the proof, and issues <c>token_type=DPoP</c> to a login that sent <c>dpop_jkt</c>.
    /// </summary>
    public bool DPoP { get; init; }

    /// <summary>
    /// With <see cref="DPoP"/>: the userinfo endpoint answers <c>use_dpop_nonce</c> with this nonce to a proof
    /// without it (RFC 9449 section 9).
    /// </summary>
    public string? UserInfoNonce { get; init; }

    /// <summary>
    /// With <see cref="DPoP"/>: the userinfo endpoint does not know the DPoP scheme, as Duende IdentityServer. It
    /// answers it with 401 and only a Bearer challenge, and accepts a bound token as Bearer.
    /// </summary>
    public bool UserInfoBearerOnly { get; init; }

    /// <summary>
    /// The discovery document has an <c>issuer</c> that is not the issuer URL of the provider (OIDC Discovery 4.3).
    /// </summary>
    public bool MismatchedDiscoveryIssuer { get; init; }

    public string[] TokenEndpointAuthMethods { get; init; } = ["client_secret_basic", "client_secret_post"];
}
