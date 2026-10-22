using System.Text.Json;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace FullstackTemplate.Bff.Features;

/// <summary>
/// The <c>Auth:ClientAuthentication</c> setting: how the BFF authenticates at the token endpoint.
/// </summary>
public enum ClientAuthenticationMethod
{
    /// <summary>
    /// <c>client_secret_basic</c>, unless discovery lists only <c>client_secret_post</c>.
    /// </summary>
    Auto,

    /// <summary>
    /// <c>client_secret_basic</c> (RFC 6749 section 2.3.1).
    /// </summary>
    Basic,

    /// <summary>
    /// <c>client_secret_post</c>.
    /// </summary>
    Post,
}

/// <summary>
/// The rule for the <c>iss</c> parameter of an authorization response (RFC 9207 section 2.4).
/// </summary>
public enum IssuerParameterRule
{
    /// <summary>
    /// No check.
    /// </summary>
    Disabled,

    /// <summary>
    /// The response must have an <c>iss</c>, and it must equal the issuer.
    /// </summary>
    Required,

    /// <summary>
    /// A response with no <c>iss</c> passes. A present <c>iss</c> must equal the issuer.
    /// </summary>
    IfPresent,
}

/// <summary>
/// The features that the BFF uses with this identity provider. The BFF decides them one time at startup, from the
/// <c>Auth:Features</c> settings and the discovery document. A change of the provider features needs a restart.
/// </summary>
/// <param name="Issuer">The <c>issuer</c> of the discovery document.</param>
/// <param name="Revocation">Logout revokes the refresh token.</param>
/// <param name="IssuerParameter">The rule for the <c>iss</c> parameter of the callback.</param>
/// <param name="BackChannelLogout"><c>/bff/backchannel</c> accepts logout tokens.</param>
/// <param name="EndSession">Logout sends the browser to the <c>end_session_endpoint</c>.</param>
/// <param name="PushedAuthorization">A login pushes the authorize request (RFC 9126).</param>
/// <param name="ClientAuthentication">The client authentication at the token endpoint: Basic or Post.</param>
/// <param name="DPoP">The BFF binds the tokens to its DPoP key (RFC 9449) and sends DPoP proofs.</param>
public sealed record ActiveFeatures(
    string Issuer,
    bool Revocation,
    IssuerParameterRule IssuerParameter,
    bool BackChannelLogout,
    bool EndSession,
    bool PushedAuthorization,
    ClientAuthenticationMethod ClientAuthentication,
    bool DPoP = false);

/// <summary>
/// A feature that the identity provider does not offer, under <c>Auto</c>. The BFF logs one startup warning for it.
/// </summary>
/// <param name="Feature">The name of the setting under <c>Auth:Features</c>, or <c>ClientAuthentication</c>.</param>
/// <param name="DiscoveryField">The discovery field that does not list the feature.</param>
/// <param name="Fallback">What the BFF does in place of the feature.</param>
public sealed record FeatureWarning(string Feature, string DiscoveryField, string Fallback);

/// <summary>
/// The decision of the active features (ADR 0005). <c>Auto</c> uses a feature if discovery lists it, <c>Require</c>
/// stops the BFF if discovery does not list it, and <c>Disable</c> never uses it.
/// </summary>
public static class ProviderFeatureDecision
{
    public const string BackChannelLogoutSupportedField = "backchannel_logout_supported";

    public const string DPoPAlgorithmsField = "dpop_signing_alg_values_supported";

    public static (ActiveFeatures Features, IReadOnlyList<FeatureWarning> Warnings) Decide(
        OpenIdConnectConfiguration discovery,
        ProviderFeatureOptions options,
        ClientAuthenticationMethod clientAuthentication,
        string? logoutEndpoint = null,
        string? dpopKeyAlgorithm = null)
    {
        var warnings = new List<FeatureWarning>();

        var revocation = Decide(options.Revocation, !string.IsNullOrEmpty(discovery.RevocationEndpoint), warnings,
            new FeatureWarning(nameof(options.Revocation), "revocation_endpoint", "Logout does not revoke the refresh token."));

        var issuerParameter = Decide(options.IssuerParameter, discovery.AuthorizationResponseIssParameterSupported, warnings,
            new FeatureWarning(nameof(options.IssuerParameter), "authorization_response_iss_parameter_supported",
                "A callback with no iss passes. A present iss must still match."));

        // With no backchannel_logout_supported, the endpoint stays mapped and validates each token
        Decide(options.BackChannelLogout, IsTrue(discovery, BackChannelLogoutSupportedField), warnings,
            new FeatureWarning(nameof(options.BackChannelLogout), BackChannelLogoutSupportedField,
                "/bff/backchannel stays mapped and validates each logout token."));

        // Auth:LogoutEndpoint stands in for a logout endpoint that discovery does not list, as with Cognito
        var endSession = Decide(options.EndSession,
            !string.IsNullOrEmpty(discovery.EndSessionEndpoint) || !string.IsNullOrEmpty(logoutEndpoint), warnings,
            new FeatureWarning(nameof(options.EndSession), "end_session_endpoint",
                "Logout ends the BFF session and sends the browser to the local returnUrl."));

        var pushedAuthorization = Decide(options.PushedAuthorization, !string.IsNullOrEmpty(discovery.PushedAuthorizationRequestEndpoint),
            warnings, new FeatureWarning(nameof(options.PushedAuthorization), "pushed_authorization_request_endpoint",
                "A login uses the plain authorize URL."));

        // DPoP needs the key of the BFF, and discovery must list the algorithm of that key
        var dpop = Decide(options.DPoP, dpopKeyAlgorithm is not null && discovery.DPoPSigningAlgValuesSupported.Contains(dpopKeyAlgorithm), warnings,
            new FeatureWarning(nameof(options.DPoP), $"{DPoPAlgorithmsField} with the alg of Auth:DPoPJsonWebKey",
                "The tokens are Bearer tokens."));

        var features = new ActiveFeatures(
            Issuer: discovery.Issuer,
            Revocation: revocation,
            IssuerParameter: options.IssuerParameter switch
            {
                FeatureMode.Disable => IssuerParameterRule.Disabled,
                _ when issuerParameter => IssuerParameterRule.Required,
                _ => IssuerParameterRule.IfPresent,
            },
            BackChannelLogout: options.BackChannelLogout != FeatureMode.Disable,
            EndSession: endSession,
            PushedAuthorization: pushedAuthorization,
            ClientAuthentication: DecideClientAuthentication(clientAuthentication, discovery.TokenEndpointAuthMethodsSupported, warnings),
            DPoP: dpop);

        return (features, warnings);
    }

    /// <summary>
    /// True when the feature is active. Throws for <c>Require</c> when discovery does not list the feature, and adds
    /// the warning for <c>Auto</c>.
    /// </summary>
    private static bool Decide(FeatureMode mode, bool listed, List<FeatureWarning> warnings, FeatureWarning missing)
    {
        switch (mode)
        {
            case FeatureMode.Disable:
                return false;
            case FeatureMode.Require when !listed:
                throw new ProviderFeatureException(
                    $"Auth:Features:{missing.Feature} is Require, but the discovery document does not list {missing.DiscoveryField}.");
            case FeatureMode.Auto when !listed:
                warnings.Add(missing);
                return false;
            default:
                return true;
        }
    }

    private static ClientAuthenticationMethod DecideClientAuthentication(
        ClientAuthenticationMethod setting,
        ICollection<string> supported,
        List<FeatureWarning> warnings)
    {
        if (setting != ClientAuthenticationMethod.Auto)
            return setting;

        // A missing field means client_secret_basic (OpenID Connect Discovery 1.0 section 3)
        if (supported.Count == 0 || supported.Contains("client_secret_basic"))
            return ClientAuthenticationMethod.Basic;

        if (supported.Contains("client_secret_post"))
        {
            warnings.Add(new FeatureWarning("ClientAuthentication", "client_secret_basic in token_endpoint_auth_methods_supported",
                "The token requests use client_secret_post, because discovery does not list client_secret_basic."));
            return ClientAuthenticationMethod.Post;
        }

        warnings.Add(new FeatureWarning("ClientAuthentication", "client_secret_basic in token_endpoint_auth_methods_supported",
            "The token requests use client_secret_basic, because discovery lists neither client_secret_basic nor client_secret_post."));
        return ClientAuthenticationMethod.Basic;
    }

    /// <summary>
    /// True when a boolean field that <see cref="OpenIdConnectConfiguration"/> does not model is <c>true</c>.
    /// </summary>
    private static bool IsTrue(OpenIdConnectConfiguration discovery, string field) =>
        discovery.AdditionalData.TryGetValue(field, out var value) && value switch
        {
            bool flag => flag,
            JsonElement { ValueKind: JsonValueKind.True } => true,
            string text => bool.TryParse(text, out var parsed) && parsed,
            _ => false,
        };
}

/// <summary>
/// A feature setting that the identity provider cannot meet. The BFF stops at startup.
/// </summary>
public sealed class ProviderFeatureException(string message) : InvalidOperationException(message);
