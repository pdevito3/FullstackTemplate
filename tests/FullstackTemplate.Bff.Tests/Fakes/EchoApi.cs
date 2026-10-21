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
/// response set cookies.
/// <para>
/// Two paths fail on purpose: <c>/api/test/hang</c> answers only when the caller goes away, and
/// <c>/api/test/drop/{id}</c> counts the request and then drops the connection with no response.
/// </para>
/// </summary>
public sealed class EchoApi : IAsyncDisposable
{
    public const string SetCookieParameter = "test_set_cookie";

    private readonly WebApplication _app;
    private readonly ConcurrentDictionary<string, int> _drops = new();

    private EchoApi(WebApplication app) => _app = app;

    public string Url { get; private set; } = "";

    /// <summary>
    /// The number of requests to <c>/api/test/drop/{id}</c> with <paramref name="id"/>.
    /// </summary>
    public int DropCount(string id) => _drops.GetValueOrDefault(id);

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
            // Each test_set_cookie query value becomes one Set-Cookie header of the response
            foreach (var cookie in request.Query[SetCookieParameter])
                response.Headers.Append("Set-Cookie", cookie);

            return Results.Json(new EchoedRequest(
                request.Method,
                request.Path.Value ?? "",
                request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase)));
        });

        await app.StartAsync();
        api.Url = app.Services.GetRequiredService<IServer>().Features.GetRequiredFeature<IServerAddressesFeature>()
            .Addresses.Single().TrimEnd('/');
        return api;
    }

    public ValueTask DisposeAsync() => _app.DisposeAsync();
}

public sealed record EchoedRequest(string Method, string Path, Dictionary<string, string> Headers);
