using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace FullstackTemplate.Bff.Authentication;

/// <summary>
/// The settings of the calls from the BFF to the identity provider: discovery, the signing keys, and the token
/// endpoint. Only the conformance job changes them (section 7 of docs/specs/home-rolled-bff.md).
/// </summary>
public static class IdentityProviderBackchannel
{
    /// <summary>
    /// The shortest time between two refreshes of the discovery document and the signing keys (minimum 1 second).
    /// A signing key that the BFF does not know causes a refresh. The default is the handler default.
    /// </summary>
    public const string MetadataRefreshIntervalKey = "Auth:MetadataRefreshInterval";

    /// <summary>
    /// A PEM file with one certificate. The BFF accepts exactly this certificate from the identity provider, also
    /// when its chain or its name is not valid. For a test provider with a self-signed certificate, such as the
    /// OpenID conformance suite. Each other certificate gets the normal checks.
    /// </summary>
    public const string PinnedCertificatePathKey = "Auth:PinnedCertificatePath";

    /// <summary>
    /// Applies the settings to the OIDC handler options. Call it before token management wraps the backchannel
    /// handler.
    /// </summary>
    public static void Configure(OpenIdConnectOptions options, IConfiguration configuration)
    {
        if (configuration.GetValue<TimeSpan?>(MetadataRefreshIntervalKey) is { } refreshInterval)
            options.RefreshInterval = refreshInterval;

        if (configuration[PinnedCertificatePathKey] is { Length: > 0 } path)
        {
            var pinned = X509Certificate2.CreateFromPem(File.ReadAllText(path));
            options.BackchannelHttpHandler = new SocketsHttpHandler
            {
                SslOptions = { RemoteCertificateValidationCallback = (_, certificate, _, errors) => IsTrusted(certificate, errors, pinned) },
            };
        }
    }

    /// <summary>
    /// True when the certificate passes the normal checks, or when it is exactly the pinned certificate.
    /// </summary>
    public static bool IsTrusted(X509Certificate? certificate, SslPolicyErrors errors, X509Certificate2 pinned) =>
        errors == SslPolicyErrors.None
        || (certificate is not null && certificate.GetRawCertData().AsSpan().SequenceEqual(pinned.RawData));
}
