namespace FullstackTemplate.Bff.Tests.Fakes;

using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// A stand-in for the server API on a real port. Each request gets its method, path, and headers back as JSON,
/// so a test sees exactly what the BFF proxy forwarded. The query parameter <see cref="SetCookieParameter"/> makes the
/// response set cookies. The query parameter <see cref="RequireNonceParameter"/> makes a request with
/// <c>Authorization: DPoP</c> get <c>401 use_dpop_nonce</c> until its proof has that nonce (RFC 9449 section 9). With
/// <see cref="RejectNonceParameter"/> as well, the API never accepts the proof.
/// <para>
/// Two paths fail on purpose: <c>/api/test/hang</c> answers only when the caller goes away, and
/// <c>/api/test/drop/{id}</c> counts the request and then drops the connection with no response.
/// </para>
/// </summary>
public sealed class EchoApi : IAsyncDisposable
{
    public const string SetCookieParameter = "test_set_cookie";
    public const string RequireNonceParameter = "test_require_nonce";
    public const string RejectNonceParameter = "test_reject_nonce";

    private readonly WebApplication _app;
    private readonly ConcurrentDictionary<string, int> _drops = new();

    private EchoApi(WebApplication app) => _app = app;

    public string Url { get; private set; } = "";

    /// <summary>
    /// The number of requests to <c>/api/test/drop/{id}</c> with <paramref name="id"/>.
    /// </summary>
    public int DropCount(string id) => _drops.GetValueOrDefault(id);

    /// <summary>
    /// The path and headers of each request, in order, also of the requests that answered 401.
    /// </summary>
    public ConcurrentQueue<EchoedRequest> Requests { get; } = new();

    public static async Task<EchoApi> StartAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var app = builder.Build();
        var api = new EchoApi(app);

        app.Map("/api/test/hang", async (HttpContext context) =>
        {
            try
            {
                await Task.Delay(Timeout.Infinite, context.RequestAborted);
            }
            catch (OperationCanceledException)
            {
                // The caller went away
            }

            return Results.Empty;
        });
        app.Map("/api/test/drop/{id}", (HttpContext context, string id) =>
        {
            api._drops.AddOrUpdate(id, 1, (_, count) => count + 1);
            context.Abort();
            return Results.Empty;
        });

        app.Map("/{**path}", (HttpRequest request, HttpResponse response) =>
        {
            var echoed = new EchoedRequest(
                request.Method,
                request.Path.Value ?? "",
                request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase));
            api.Requests.Enqueue(echoed);

            if (request.Query[RequireNonceParameter].FirstOrDefault() is { } nonce
                && request.Headers.Authorization.ToString().StartsWith("DPoP ", StringComparison.Ordinal)
                && (request.Query.ContainsKey(RejectNonceParameter) || ProofNonce(request.Headers["DPoP"].ToString()) != nonce))
            {
                response.Headers["DPoP-Nonce"] = nonce;
                response.Headers.WWWAuthenticate = "DPoP error=\"use_dpop_nonce\", algs=\"ES256\"";
                return Results.StatusCode(StatusCodes.Status401Unauthorized);
            }

            // Each test_set_cookie query value becomes one Set-Cookie header of the response
            foreach (var cookie in request.Query[SetCookieParameter])
                response.Headers.Append("Set-Cookie", cookie);

            return Results.Json(echoed);
        });

        await app.StartAsync();
        api.Url = app.Services.GetRequiredService<IServer>().Features.GetRequiredFeature<IServerAddressesFeature>()
            .Addresses.Single().TrimEnd('/');
        return api;
    }

    // Reads the nonce claim of a proof without checking it
    private static string? ProofNonce(string proof)
    {
        try
        {
            using var payload = System.Text.Json.JsonDocument.Parse(Microsoft.IdentityModel.Tokens.Base64UrlEncoder.DecodeBytes(proof.Split('.')[1]));
            return payload.RootElement.TryGetProperty("nonce", out var nonce) ? nonce.GetString() : null;
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or System.Text.Json.JsonException or FormatException)
        {
            return null;
        }
    }

    public ValueTask DisposeAsync() => _app.DisposeAsync();
}

public sealed record EchoedRequest(string Method, string Path, Dictionary<string, string> Headers);
