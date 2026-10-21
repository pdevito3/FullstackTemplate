namespace FullstackTemplate.Bff.Tests.Fakes;

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
/// so a test sees exactly what the BFF proxy forwarded.
/// </summary>
public sealed class EchoApi : IAsyncDisposable
{
    private readonly WebApplication _app;

    private EchoApi(WebApplication app) => _app = app;

    public string Url { get; private set; } = "";

    public static async Task<EchoApi> StartAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var app = builder.Build();

        app.Map("/{**path}", (HttpRequest request) => Results.Json(new EchoedRequest(
            request.Method,
            request.Path.Value ?? "",
            request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase))));

        await app.StartAsync();
        var api = new EchoApi(app)
        {
            Url = app.Services.GetRequiredService<IServer>().Features.GetRequiredFeature<IServerAddressesFeature>()
                .Addresses.Single().TrimEnd('/'),
        };
        return api;
    }

    public ValueTask DisposeAsync() => _app.DisposeAsync();
}

public sealed record EchoedRequest(string Method, string Path, Dictionary<string, string> Headers);
