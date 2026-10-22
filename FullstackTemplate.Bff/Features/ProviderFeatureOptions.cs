using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace FullstackTemplate.Bff.Features;

/// <summary>
/// The value of an <c>Auth:Features:*</c> setting.
/// </summary>
public enum FeatureMode
{
    /// <summary>
    /// Use the feature if discovery lists it. If not, use the safe fallback and log a startup warning.
    /// </summary>
    Auto,

    /// <summary>
    /// Use the feature. The BFF stops at startup if discovery does not list it.
    /// </summary>
    Require,

    /// <summary>
    /// Never use the feature.
    /// </summary>
    Disable,
}

/// <summary>
/// The <c>Auth:Features</c> settings. The BFF decides the active features from these settings and the discovery
/// document at startup (<see cref="ProviderFeatureDiscovery"/>).
/// </summary>
public sealed class ProviderFeatureOptions
{
    public const string SectionName = "Auth:Features";

    /// <summary>
    /// Refresh token revocation (RFC 7009), from <c>revocation_endpoint</c>.
    /// </summary>
    public FeatureMode Revocation { get; set; } = FeatureMode.Auto;

    /// <summary>
    /// The <c>iss</c> authorization response parameter (RFC 9207), from
    /// <c>authorization_response_iss_parameter_supported</c>.
    /// </summary>
    public FeatureMode IssuerParameter { get; set; } = FeatureMode.Auto;

    /// <summary>
    /// Back-channel logout, from <c>backchannel_logout_supported</c>.
    /// </summary>
    public FeatureMode BackChannelLogout { get; set; } = FeatureMode.Auto;

    /// <summary>
    /// RP-initiated logout, from <c>end_session_endpoint</c>.
    /// </summary>
    public FeatureMode EndSession { get; set; } = FeatureMode.Auto;

    /// <summary>
    /// Pushed authorization requests (RFC 9126), from <c>pushed_authorization_request_endpoint</c>.
    /// </summary>
    public FeatureMode PushedAuthorization { get; set; } = FeatureMode.Auto;

    /// <summary>
    /// DPoP (RFC 9449), from <c>dpop_signing_alg_values_supported</c>. It also needs the key
    /// <c>Auth:DPoPJsonWebKey</c>. <c>Auto</c> uses DPoP when the key is set and discovery lists its algorithm,
    /// <c>Require</c> stops the BFF otherwise.
    /// </summary>
    public FeatureMode DPoP { get; set; } = FeatureMode.Disable;

    /// <summary>
    /// The OIDC handler option for <see cref="PushedAuthorization"/>. The handler reads the PAR endpoint from the
    /// live discovery document.
    /// </summary>
    public static PushedAuthorizationBehavior ToPushedAuthorizationBehavior(FeatureMode mode) => mode switch
    {
        FeatureMode.Auto => PushedAuthorizationBehavior.UseIfAvailable,
        FeatureMode.Require => PushedAuthorizationBehavior.Require,
        FeatureMode.Disable => PushedAuthorizationBehavior.Disable,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };
}
