namespace FullstackTemplate.IntegrationTests.DPoP;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FullstackTemplate.Server.Resources.Entrypoints.DPoP;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// A small server with the real JWT bearer handler, the real DPoP code, and the real replay cache table in the
/// test database. The access tokens are signed with a symmetric test key.
/// </summary>
public sealed class DPoPTestHost : IAsyncDisposable
{
    public static readonly DateTimeOffset Now = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);
    public const string BaseUrl = "http://localhost";
    public const string ProtectedPath = "/protected";

    private static readonly SymmetricSecurityKey AccessTokenKey = new(Encoding.UTF8.GetBytes("a-test-signing-key-of-at-least-32-bytes!"));

    private readonly WebApplication _app;

    public HttpClient Client { get; }
    public TestTimeProvider Time { get; }

    private DPoPTestHost(WebApplication app, TestTimeProvider time)
    {
        _app = app;
        Time = time;
        Client = app.GetTestClient();
    }

    public static async Task<DPoPTestHost> StartAsync()
    {
        var time = new TestTimeProvider(Now);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<TimeProvider>(time);
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = false,
                    IssuerSigningKey = AccessTokenKey,
                };
            });
        builder.Services.AddAuthorization();
        builder.Services.AddDPoP(new ConfigurationBuilder().Build());
        // The replay cache of the test host is the real one, on the database of the assembly fixture
        builder.Services.AddScoped<IDPoPReplayCache, FixtureReplayCache>();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapMethods("/{**path}", ["GET", "POST"], (HttpContext context) => Results.Ok(new { sub = context.User.FindFirst("sub")?.Value }))
            .RequireAuthorization();
        await app.StartAsync();

        return new DPoPTestHost(app, time);
    }

    /// <summary>
    /// A signed access token. <paramref name="boundTo"/> adds the <c>cnf.jkt</c> claim.
    /// </summary>
    public static string AccessToken(string? boundTo = null)
    {
        var claims = new Dictionary<string, object> { ["sub"] = "user-1", ["jti"] = Guid.NewGuid().ToString("N") };
        if (boundTo is not null)
            claims["cnf"] = new Dictionary<string, object> { ["jkt"] = boundTo };

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Claims = claims,
            SigningCredentials = new SigningCredentials(AccessTokenKey, SecurityAlgorithms.HmacSha256),
        });
    }

    public HttpRequestMessage Request(string authorizationScheme, string accessToken, string? proof, HttpMethod? method = null, string path = ProtectedPath)
    {
        var request = new HttpRequestMessage(method ?? HttpMethod.Get, new Uri(Client.BaseAddress!, path));
        request.Headers.TryAddWithoutValidation("Authorization", $"{authorizationScheme} {accessToken}");
        if (proof is not null)
            request.Headers.TryAddWithoutValidation("DPoP", proof);
        return request;
    }

    public ValueTask DisposeAsync() => _app.DisposeAsync();

    private sealed class FixtureReplayCache : IDPoPReplayCache
    {
        public async Task<bool> TryAddAsync(string jwkThumbprint, string jti, DateTimeOffset expires, CancellationToken cancellationToken)
        {
            await using var scope = TestFixture.BaseScopeFactory.CreateAsyncScope();
            return await new DPoPReplayCache(scope.ServiceProvider.GetRequiredService<MediatR.ISender>())
                .TryAddAsync(jwkThumbprint, jti, expires, cancellationToken);
        }

        public async Task<int> DeleteExpiredAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken)
        {
            await using var scope = TestFixture.BaseScopeFactory.CreateAsyncScope();
            return await new DPoPReplayCache(scope.ServiceProvider.GetRequiredService<MediatR.ISender>())
                .DeleteExpiredAsync(now, batchSize, cancellationToken);
        }
    }
}

/// <summary>A clock that a test can move.</summary>
public sealed class TestTimeProvider(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;
    public void Advance(TimeSpan by) => _now += by;
    public override DateTimeOffset GetUtcNow() => _now;
}

/// <summary>
/// Makes DPoP proofs by hand, so that a test can break exactly one rule of RFC 9449 section 4.3.
/// </summary>
public sealed class DPoPProofBuilder : IDisposable
{
    public ECDsa Key { get; } = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public Dictionary<string, object?> Header { get; }
    public Dictionary<string, object?> Payload { get; } = new();

    /// <summary>The key that signs the proof. The default is <see cref="Key"/>, the key of the jwk header.</summary>
    public ECDsa? SigningKey { get; set; }

    public DPoPProofBuilder(string accessToken, string method = "GET", string url = DPoPTestHost.BaseUrl + DPoPTestHost.ProtectedPath)
    {
        Header = new Dictionary<string, object?>
        {
            ["typ"] = "dpop+jwt",
            ["alg"] = "ES256",
            ["jwk"] = PublicJwk(Key),
        };
        Payload["jti"] = Guid.NewGuid().ToString("N");
        Payload["htm"] = method;
        Payload["htu"] = url;
        Payload["iat"] = DPoPTestHost.Now.ToUnixTimeSeconds();
        Payload["ath"] = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(accessToken)));
    }

    public string Thumbprint => Base64UrlEncoder.Encode(new JsonWebKey(JsonSerializer.Serialize(PublicJwk(Key))).ComputeJwkThumbprint());

    public static Dictionary<string, object?> PublicJwk(ECDsa key)
    {
        var parameters = key.ExportParameters(false);
        return new Dictionary<string, object?>
        {
            ["kty"] = "EC",
            ["crv"] = "P-256",
            ["x"] = Base64UrlEncoder.Encode(parameters.Q.X),
            ["y"] = Base64UrlEncoder.Encode(parameters.Q.Y),
        };
    }

    public string Build()
    {
        var signingInput = $"{Encode(Header)}.{Encode(Payload)}";
        var alg = Header["alg"] as string;
        var signature = alg == "ES256"
            ? (SigningKey ?? Key).SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)
            : alg == "HS256"
                ? HMACSHA256.HashData(Encoding.UTF8.GetBytes("a-test-signing-key-of-at-least-32-bytes!"), Encoding.ASCII.GetBytes(signingInput))
                : [];
        return $"{signingInput}.{Base64UrlEncoder.Encode(signature)}";
    }

    private static string Encode(object value) => Base64UrlEncoder.Encode(JsonSerializer.SerializeToUtf8Bytes(value));

    public void Dispose()
    {
        Key.Dispose();
        SigningKey?.Dispose();
    }
}
