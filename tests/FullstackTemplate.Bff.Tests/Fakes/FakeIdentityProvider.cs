namespace FullstackTemplate.Bff.Tests.Fakes;

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// A minimal OpenID Connect provider on a real port, so that the OIDC handler and token management of the BFF
/// talk to it over HTTP as they talk to a real provider.
/// <para>
/// The authorize endpoint reads test-only query parameters, so that each test chooses its own user:
/// <c>test_sub</c>, <c>test_sid</c>, <c>test_expires_in</c>, <c>test_session_state</c>, <c>test_error</c>,
/// <c>test_refresh</c> (<c>fail</c> makes each refresh return <c>invalid_grant</c>), and <c>test_revoke</c> (<c>fail</c>
/// makes each revocation of a refresh token of the user return 500, and <c>drop</c> closes the connection).
/// A refresh response has no <c>id_token</c>.
/// </para>
/// </summary>
public sealed class FakeIdentityProvider : IAsyncDisposable
{
    public const string ClientId = "bff-client";
    public const string ClientSecret = "bff-secret";
    public static readonly string[] PromptValues = ["none", "login", "consent"];
    private static readonly string[] ResponseTypes = ["code"];
    private static readonly string[] SubjectTypes = ["public"];
    private static readonly string[] SigningAlgorithms = ["RS256"];

    private readonly WebApplication _app;
    // A key from parameters, not from one RSA instance: each signature provider then gets its own RSA instance,
    // so that parallel tests do not sign with one RSA instance at the same time, which can give a bad signature
    private readonly RsaSecurityKey _signingKey = new(CreateRsaParameters()) { KeyId = "test-key" };
    private readonly ConcurrentDictionary<string, Grant> _codes = new();
    private readonly ConcurrentDictionary<string, Grant> _accessTokens = new();
    private readonly ConcurrentDictionary<string, Grant> _refreshTokens = new();
    private readonly ConcurrentDictionary<string, RequestGate> _refreshGates = new();
    private readonly ConcurrentDictionary<string, RequestGate> _revocationGates = new();
    private readonly ConcurrentQueue<IssuedTokens> _issued = new();
    private readonly ConcurrentDictionary<string, string> _revokeModes = new();

    private FakeIdentityProvider(WebApplication app) => _app = app;

    public string Issuer { get; private set; } = "";

    /// <summary>
    /// Each token sent to the revocation endpoint.
    /// </summary>
    public ConcurrentBag<string> RevokedTokens { get; } = [];

    /// <summary>
    /// Each refresh token sent to the token endpoint with <c>grant_type=refresh_token</c>.
    /// </summary>
    public ConcurrentBag<string> RefreshRequests { get; } = [];

    private static RSAParameters CreateRsaParameters()
    {
        using var rsa = RSA.Create(2048);
        return rsa.ExportParameters(includePrivateParameters: true);
    }

    public SigningCredentials SigningCredentials => new(_signingKey, SecurityAlgorithms.RsaSha256);

    public static async Task<FakeIdentityProvider> StartAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var app = builder.Build();
        var provider = new FakeIdentityProvider(app);
        provider.MapEndpoints();

        await app.StartAsync();
        provider.Issuer = app.Services.GetRequiredService<IServer>().Features.GetRequiredFeature<IServerAddressesFeature>()
            .Addresses.Single().TrimEnd('/');
        return provider;
    }

    /// <summary>
    /// Signs a JWT with the key of the provider, or with <paramref name="credentials"/>. With
    /// <paramref name="withoutSignature"/>, the JWT has <c>alg=none</c> and no signature. With no
    /// <paramref name="tokenType"/>, the header has no <c>typ</c>.
    /// </summary>
    public string CreateJwt(Dictionary<string, object> claims, string? tokenType = null, SigningCredentials? credentials = null,
        bool withoutSignature = false)
    {
        var signing = withoutSignature ? null : credentials ?? SigningCredentials;
        var handler = new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false };
        var token = handler.CreateToken(new SecurityTokenDescriptor
        {
            Claims = claims,
            SigningCredentials = signing,
            TokenType = tokenType,
        });
        return tokenType is null && signing is not null ? WithoutTyp(token, signing) : token;
    }

    // JsonWebTokenHandler always writes typ=JWT when no type is given, so sign the payload again with a header without it
    private static string WithoutTyp(string token, SigningCredentials signing)
    {
        var header = Base64UrlEncoder.Encode(JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["alg"] = signing.Algorithm,
            ["kid"] = signing.Key.KeyId,
        }));
        var signingInput = $"{header}.{token.Split('.')[1]}";
        var factory = signing.CryptoProviderFactory ?? signing.Key.CryptoProviderFactory;
        var provider = factory.CreateForSigning(signing.Key, signing.Algorithm);
        try
        {
            var signature = provider.Sign(System.Text.Encoding.ASCII.GetBytes(signingInput));
            return $"{signingInput}.{Base64UrlEncoder.Encode(signature)}";
        }
        finally
        {
            factory.ReleaseSignatureProvider(provider);
        }
    }

    /// <summary>
    /// True when the revocation endpoint got a refresh token that the provider issued to <paramref name="subject"/>.
    /// </summary>
    public bool WasRefreshTokenRevoked(string subject) =>
        RevokedTokens.Any(t => _refreshTokens.TryGetValue(t, out var grant) && grant.Subject == subject);

    /// <summary>
    /// The number of refresh requests for the refresh tokens of <paramref name="subject"/>.
    /// </summary>
    public int RefreshCount(string subject) =>
        RefreshRequests.Count(t => _refreshTokens.TryGetValue(t, out var grant) && grant.Subject == subject);

    /// <summary>
    /// The refresh tokens that the provider issued to <paramref name="subject"/>, in order.
    /// </summary>
    public IReadOnlyList<string> IssuedRefreshTokens(string subject) =>
        _issued.Where(t => t.Subject == subject).Select(t => t.RefreshToken).ToList();

    /// <summary>
    /// The ID tokens that the provider issued to <paramref name="subject"/>, in order.
    /// </summary>
    public IReadOnlyList<string> IssuedIdTokens(string subject) =>
        _issued.Where(t => t.Subject == subject && t.IdToken is not null).Select(t => t.IdToken!).ToList();

    /// <summary>
    /// Holds each refresh request of <paramref name="subject"/> until the test releases the gate. The gate tells
    /// the test when a refresh request arrives.
    /// </summary>
    public RequestGate HoldRefreshes(string subject) => _refreshGates.GetOrAdd(subject, _ => new RequestGate());

    /// <summary>
    /// Holds each revocation request for a refresh token of <paramref name="subject"/> until the test releases the
    /// gate. The gate tells the test when a revocation request arrives.
    /// </summary>
    public RequestGate HoldRevocations(string subject) => _revocationGates.GetOrAdd(subject, _ => new RequestGate());

    public ValueTask DisposeAsync() => _app.DisposeAsync();

    private void MapEndpoints()
    {
        _app.MapGet("/.well-known/openid-configuration", () => Results.Json(new Dictionary<string, object>
        {
            ["issuer"] = Issuer,
            ["authorization_endpoint"] = $"{Issuer}/authorize",
            ["token_endpoint"] = $"{Issuer}/token",
            ["userinfo_endpoint"] = $"{Issuer}/userinfo",
            ["jwks_uri"] = $"{Issuer}/jwks",
            ["end_session_endpoint"] = $"{Issuer}/endsession",
            ["revocation_endpoint"] = $"{Issuer}/revoke",
            ["response_types_supported"] = ResponseTypes,
            ["subject_types_supported"] = SubjectTypes,
            ["id_token_signing_alg_values_supported"] = SigningAlgorithms,
            ["prompt_values_supported"] = PromptValues,
        }));

        _app.MapGet("/jwks", () =>
        {
            var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(_signingKey);
            return Results.Json(new
            {
                keys = new[] { new { kty = jwk.Kty, use = "sig", kid = jwk.Kid, alg = "RS256", n = jwk.N, e = jwk.E } },
            });
        });

        _app.MapGet("/authorize", (HttpRequest request) =>
        {
            var query = request.Query;
            var redirectUri = query["redirect_uri"].ToString();
            var state = Uri.EscapeDataString(query["state"].ToString());

            if (query["test_error"].FirstOrDefault() is { } error)
                return Results.Redirect($"{redirectUri}?error={Uri.EscapeDataString(error)}&state={state}");

            var grant = new Grant(
                Subject: query["test_sub"].FirstOrDefault() ?? "user-1",
                SessionId: query["test_sid"].FirstOrDefault(),
                Nonce: query["nonce"].ToString(),
                ExpiresIn: int.TryParse(query["test_expires_in"], out var expiresIn) ? expiresIn : 3600,
                FailRefresh: query["test_refresh"] == "fail");

            if (query["test_revoke"].FirstOrDefault() is { } revokeMode)
                _revokeModes[grant.Subject] = revokeMode;

            var code = NewToken("code");
            _codes[code] = grant;

            var location = $"{redirectUri}?code={code}&state={state}";
            if (query["test_session_state"].FirstOrDefault() is { } sessionState)
                location += $"&session_state={Uri.EscapeDataString(sessionState)}";

            return Results.Redirect(location);
        });

        _app.MapPost("/token", async (HttpRequest request) =>
        {
            var form = await request.ReadFormAsync();
            switch (form["grant_type"].ToString())
            {
                case "authorization_code" when _codes.TryRemove(form["code"].ToString(), out var grant):
                    return IssueTokens(grant, includeIdToken: true);

                case "refresh_token" when _refreshTokens.TryGetValue(form["refresh_token"].ToString(), out var grant):
                    RefreshRequests.Add(form["refresh_token"].ToString());
                    if (_refreshGates.TryGetValue(grant.Subject, out var gate))
                    {
                        gate.Arrived.TrySetResult();
                        await gate.Released.Task;
                    }

                    return grant.FailRefresh
                        ? Results.Json(new { error = "invalid_grant" }, statusCode: StatusCodes.Status400BadRequest)
                        : IssueTokens(grant, includeIdToken: false);

                default:
                    return Results.Json(new { error = "invalid_grant" }, statusCode: StatusCodes.Status400BadRequest);
            }
        });

        _app.MapMethods("/userinfo", ["GET", "POST"], (HttpRequest request) =>
        {
            var accessToken = request.Headers.Authorization.ToString().Replace("Bearer ", "", StringComparison.Ordinal);
            return _accessTokens.TryGetValue(accessToken, out var grant)
                ? Results.Json(new { sub = grant.Subject, name = $"Name of {grant.Subject}", email = $"{grant.Subject}@example.com" })
                : Results.Unauthorized();
        });

        _app.MapPost("/revoke", async (HttpContext context) =>
        {
            var form = await context.Request.ReadFormAsync();
            var token = form["token"].ToString();
            _refreshTokens.TryGetValue(token, out var grant);
            if (grant is not null && _revocationGates.TryGetValue(grant.Subject, out var gate))
            {
                gate.Arrived.TrySetResult();
                await gate.Released.Task;
            }

            RevokedTokens.Add(token);

            var mode = grant is null ? null : _revokeModes.GetValueOrDefault(grant.Subject);
            switch (mode)
            {
                case "fail":
                    return Results.StatusCode(StatusCodes.Status500InternalServerError);
                case "drop":
                    context.Abort();
                    return Results.Empty;
                default:
                    return Results.Ok();
            }
        });
    }

    private IResult IssueTokens(Grant grant, bool includeIdToken)
    {
        var accessToken = NewToken("at");
        var refreshToken = NewToken("rt");
        _accessTokens[accessToken] = grant;
        _refreshTokens[refreshToken] = grant;

        string? idToken = null;
        var response = new Dictionary<string, object>
        {
            ["access_token"] = accessToken,
            ["token_type"] = "Bearer",
            ["expires_in"] = grant.ExpiresIn,
            ["refresh_token"] = refreshToken,
        };

        if (includeIdToken)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var claims = new Dictionary<string, object>
            {
                ["iss"] = Issuer,
                ["aud"] = ClientId,
                ["sub"] = grant.Subject,
                ["nonce"] = grant.Nonce,
                ["iat"] = now,
                ["exp"] = now + 300,
            };
            if (grant.SessionId is not null)
                claims["sid"] = grant.SessionId;

            idToken = CreateJwt(claims, "JWT");
            response["id_token"] = idToken;
        }

        _issued.Enqueue(new IssuedTokens(grant.Subject, refreshToken, idToken));

        return Results.Text(JsonSerializer.Serialize(response), "application/json");
    }

    private static string NewToken(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private sealed record Grant(string Subject, string? SessionId, string Nonce, int ExpiresIn, bool FailRefresh);

    private sealed record IssuedTokens(string Subject, string RefreshToken, string? IdToken);

    /// <summary>
    /// See <see cref="HoldRefreshes"/> and <see cref="HoldRevocations"/>.
    /// </summary>
    public sealed class RequestGate
    {
        public TaskCompletionSource Arrived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Released { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
