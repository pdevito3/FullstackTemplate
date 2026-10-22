using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace FullstackTemplate.AppHost.AuthProviders;

/// <summary>
/// Creates a configuration for the OpenID conformance suite as the identity provider, for the conformance job
/// (section 7 of docs/specs/home-rolled-bff.md). The suite runs next to the stack with docker compose, see
/// frontend/conformance/run-conformance.ts. Its issuer is the URL of the test plan alias, and its certificate is
/// self-signed, so the BFF pins the certificate that the script exports to <c>Conformance:CertificatePath</c>.
/// The BFF reads the signing keys again after 1 s at the earliest, because each test module has new keys.
/// <para>The suite sends no <c>expires_in</c>, so the BFF gives each access token a lifetime of 1 s, and refreshes it
/// on the next API call. The access tokens of the suite are opaque, so the server checks them at the userinfo endpoint
/// of the suite, with the same pinned certificate. The refresh plan needs that userinfo call.</para>
/// <para>Only this repo runs the suite. scripts/init.cs deletes this folder when it renames the app.</para>
/// </summary>
public sealed class ConformanceAuthProvider : IAuthProvider
{
    /// <summary>
    /// The alias, the client id, and the client secret of the conformance test plans. frontend/conformance/
    /// run-conformance.ts gives the same values to the suite.
    /// </summary>
    public const string Alias = "fullstacktemplate-bff";
    public const string ClientId = "fullstacktemplate-bff";
    public const string ClientSecret = "conformance-secret-for-local-test-plans-only";

    // The suite logs in with no login form, and run-conformance.ts drives it in place of the e2e tests
    public bool RunsE2eTests => false;

    public AuthProviderConfig AddTo(IDistributedApplicationBuilder builder, AuthClientUrls clientUrls)
    {
        var suiteUrl = builder.Configuration["Conformance:SuiteUrl"] ?? "https://localhost.emobix.co.uk:8443";
        var certificatePath = builder.Configuration["Conformance:CertificatePath"]
            ?? Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "../frontend/conformance/.suite/suite-certificate.pem"));

        return new AuthProviderConfig
        {
            Authority = $"{suiteUrl.TrimEnd('/')}/test/a/{Alias}/",
            ClientId = ClientId,
            ClientSecret = ClientSecret,
            Audience = ClientId,
            RequireHttpsMetadata = true,
            NameClaimType = "name",
            RoleClaimType = "role",
            UseAudienceAsScope = false,
            AuthResource = null,
            BffSettings = new Dictionary<string, string>
            {
                ["Auth__MetadataRefreshInterval"] = "00:00:01",
                ["Auth__PinnedCertificatePath"] = certificatePath,
                ["Auth__DefaultAccessTokenLifetime"] = "00:00:01",
            },
            ServerSettings = new Dictionary<string, string>
            {
                ["Auth__AccessTokenValidation"] = "Userinfo",
                ["Auth__PinnedCertificatePath"] = certificatePath,
            },
            // The BFF reads the discovery document of the suite at startup, and the suite answers only while a test
            // module runs. So run-conformance.ts starts the BFF for each test module.
            ConfigureBff = bff =>
            {
                bff.WithExplicitStart();
                // The suite container calls /bff/backchannel at host.docker.internal. On Linux that is the address of
                // the Docker bridge, not loopback, so the BFF https endpoint listens on all interfaces.
                bff.WithEndpoint("https", endpoint => endpoint.TargetHost = "0.0.0.0");
            },
        };
    }
}
