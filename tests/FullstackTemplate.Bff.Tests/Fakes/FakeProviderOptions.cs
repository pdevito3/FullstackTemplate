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

    public string[] TokenEndpointAuthMethods { get; init; } = ["client_secret_basic", "client_secret_post"];
}
