namespace FullstackTemplate.Server.Resources.Entrypoints;

using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

/// <summary>
/// The settings of <see cref="UserinfoAuthenticationHandler"/>.
/// </summary>
public sealed class UserinfoAuthenticationOptions : AuthenticationSchemeOptions
{
    public string Authority { get; set; } = "";

    /// <summary><c>Auth:ClientId</c>, the client of the app.</summary>
    public string ClientId { get; set; } = "";

    public string NameClaimType { get; set; } = "name";

    public string RoleClaimType { get; set; } = "role";
}

/// <summary>
/// Accepts a bearer token when the userinfo endpoint of the identity provider accepts it, and takes the claims from
/// the userinfo response. Only for a test identity provider whose access tokens are opaque, not JWTs: the OpenID
/// conformance suite. Its refresh test plan also needs a userinfo call with the refreshed access token. Development
/// only, see <see cref="AuthExtensions.AddJwtBearerAuthentication"/>.
/// scripts/init.cs deletes this file when it renames the app.
/// </summary>
public sealed class UserinfoAuthenticationHandler(
    IOptionsMonitor<UserinfoAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IHttpClientFactory httpClientFactory)
    : AuthenticationHandler<UserinfoAuthenticationOptions>(options, logger, encoder)
{
    /// <summary>
    /// <c>Jwt</c> (the default) or <c>Userinfo</c>. With <c>Userinfo</c>, this handler checks each access token at
    /// the userinfo endpoint of the identity provider. Only the OpenID conformance suite provider of the AppHost sets
    /// it, because the suite issues opaque access tokens. Development only.
    /// </summary>
    public const string AccessTokenValidationKey = "Auth:AccessTokenValidation";

    /// <summary>
    /// A PEM file with the one certificate that the userinfo check accepts from the identity provider, such as the
    /// self-signed certificate of the conformance suite.
    /// </summary>
    public const string PinnedCertificatePathKey = "Auth:PinnedCertificatePath";

    public const string HttpClientName = "userinfo-authentication";

    /// <summary>
    /// Adds the HTTP client of the handler. With <paramref name="pinnedCertificatePath"/>, the client accepts exactly
    /// that certificate from the identity provider, also when its chain or its name is not valid, as the BFF does
    /// with <c>Auth:PinnedCertificatePath</c>. Each other certificate gets the normal checks.
    /// </summary>
    public static void AddHttpClient(IServiceCollection services, string? pinnedCertificatePath)
    {
        var client = services.AddHttpClient(HttpClientName);
        if (string.IsNullOrEmpty(pinnedCertificatePath))
            return;

        // The handler reads the file when it is made, because the conformance script writes it after the server starts
        client.ConfigurePrimaryHttpMessageHandler(() =>
        {
            var pinned = X509Certificate2.CreateFromPem(File.ReadAllText(pinnedCertificatePath));
            return new SocketsHttpHandler
            {
                SslOptions =
                {
                    RemoteCertificateValidationCallback = (_, certificate, _, errors) =>
                        errors == SslPolicyErrors.None
                        || (certificate is not null && certificate.GetRawCertData().AsSpan().SequenceEqual(pinned.RawData)),
                },
            };
        });
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!AuthenticationHeaderValue.TryParse(Request.Headers.Authorization, out var header)
            || !string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrEmpty(header.Parameter))
        {
            return AuthenticateResult.NoResult();
        }

        var client = httpClientFactory.CreateClient(HttpClientName);
        try
        {
            // The discovery document is read for each request, because the conformance suite answers only while a
            // test module runs
            using var discovery = JsonDocument.Parse(await client.GetStringAsync(
                $"{Options.Authority.TrimEnd('/')}/.well-known/openid-configuration", Context.RequestAborted));
            if (!discovery.RootElement.TryGetProperty("userinfo_endpoint", out var endpoint)
                || endpoint.GetString() is not { Length: > 0 } userinfoEndpoint)
            {
                return AuthenticateResult.Fail("The discovery document has no userinfo_endpoint.");
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, userinfoEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", header.Parameter);
            using var response = await client.SendAsync(request, Context.RequestAborted);
            if (!response.IsSuccessStatusCode)
                return AuthenticateResult.Fail($"The userinfo endpoint rejected the access token with {(int)response.StatusCode}.");

            using var userinfo = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Context.RequestAborted));
            var claims = userinfo.RootElement.EnumerateObject()
                .Where(p => p.Value.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.Null))
                .Select(p => new Claim(p.Name, p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString()! : p.Value.GetRawText()))
                .ToList();
            // A userinfo response names no client, and in this mode only the client of the app gets tokens from the
            // issuer.
            if (!claims.Any(c => c.Type is "client_id" or "azp" or "applicationId"))
                claims.Add(new Claim("client_id", Options.ClientId));
            var identity = new ClaimsIdentity(claims, Scheme.Name, Options.NameClaimType, Options.RoleClaimType);
            return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            return AuthenticateResult.Fail(ex);
        }
    }
}
