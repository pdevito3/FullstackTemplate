using System.Security.Cryptography;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace FullstackTemplate.AppHost.AuthProviders;

/// <summary>
/// Runs the stack with DPoP (RFC 9449), when the AppHost setting <c>DPoP</c> is true.
/// </summary>
public static class AuthProviderDPoP
{
    public const string SettingName = "DPoP";

    /// <summary>
    /// Whether the AppHost setting <c>DPoP</c> is true, for example the environment variable <c>DPoP=true</c>.
    /// </summary>
    public static bool IsOn(IDistributedApplicationBuilder builder) =>
        bool.TryParse(builder.Configuration[SettingName], out var on) && on;

    /// <summary>
    /// Makes the BFF require DPoP (<c>Auth:Features:DPoP</c> is Require) and gives it one EC P-256 key as the secret
    /// parameter <c>bff-dpop-key</c>. Aspire generates the value one time and saves it with the other generated
    /// secrets. Only a provider with <see cref="IAuthProvider.TestedWithDPoP"/> can run with DPoP.
    /// </summary>
    public static AuthProviderConfig WithDPoP(
        this AuthProviderConfig authProvider, IAuthProvider provider, IDistributedApplicationBuilder builder)
    {
        if (!provider.TestedWithDPoP)
        {
            var tested = string.Join(" or ", AuthProviderCatalog.All.Where(p => p.TestedWithDPoP).Select(p => p.Name));
            throw new InvalidOperationException(
                $"{SettingName}=true needs a provider that the stack tests with DPoP: {tested}. The provider is {provider.Name}.");
        }

        return authProvider with
        {
            Features = authProvider.Features with { DPoP = "Require" },
            DPoPKey = builder.AddParameter("bff-dpop-key", new DPoPKeyParameterDefault(), secret: true, persist: true),
        };
    }
}

/// <summary>
/// The generated value of the parameter <c>bff-dpop-key</c>: a new EC P-256 key as a private JSON Web Key with
/// <c>alg=ES256</c>, the format of <c>Auth:DPoPJsonWebKey</c> of the BFF.
/// </summary>
internal sealed class DPoPKeyParameterDefault : ParameterDefault
{
    public override string GetDefaultValue()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parameters = key.ExportParameters(includePrivateParameters: true);
        return JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["kty"] = "EC",
            ["crv"] = "P-256",
            ["alg"] = "ES256",
            ["use"] = "sig",
            ["x"] = Base64Url(parameters.Q.X!),
            ["y"] = Base64Url(parameters.Q.Y!),
            ["d"] = Base64Url(parameters.D!),
        });
    }

    // The key is for the local stack only, and it is never published
    public override void WriteToManifest(Aspire.Hosting.Publishing.ManifestPublishingContext context) =>
        throw new NotSupportedException("The DPoP key of the BFF is generated for the local stack only.");

    private static string Base64Url(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
