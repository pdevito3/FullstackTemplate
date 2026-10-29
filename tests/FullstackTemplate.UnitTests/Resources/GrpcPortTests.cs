namespace FullstackTemplate.UnitTests.Resources;

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Shouldly;

/// <summary>
/// Runs a real Kestrel host with the Kestrel:Endpoints of the appsettings.json of the Server. Only the URLs change
/// to port 0, so that Kestrel picks free ports. The test reads the bound port of each named endpoint after the start.
/// </summary>
[NotInParallel(TestConstraints.HostEnvironment)]
public class GrpcPortTests
{
    [Test]
    public async Task the_grpc_endpoint_serves_only_http2_and_the_http_endpoint_keeps_http1()
    {
        // Arrange
        ListenOptions? http = null;
        ListenOptions? grpc = null;
        var builder = WebApplication.CreateSlimBuilder();
        builder.Configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));
        builder.Configuration["Kestrel:Endpoints:http:Url"] = "http://127.0.0.1:0";
        builder.Configuration["Kestrel:Endpoints:grpc:Url"] = "http://127.0.0.1:0";
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.ConfigurationLoader!
            .Endpoint("http", endpoint => http = endpoint.ListenOptions)
            .Endpoint("grpc", endpoint => grpc = endpoint.ListenOptions));
        await using var app = builder.Build();
        app.MapGet("/", (HttpContext context) => context.Request.Protocol);
        await app.StartAsync();
        var httpPort = http.ShouldNotBeNull().IPEndPoint.ShouldNotBeNull().Port;
        var grpcPort = grpc.ShouldNotBeNull().IPEndPoint.ShouldNotBeNull().Port;
        using var client = new HttpClient();

        // Act
        var http1 = await client.SendAsync(Request(httpPort, HttpVersion.Version11));
        var h2c = await client.SendAsync(Request(grpcPort, HttpVersion.Version20));
        var http1OnGrpcPort = await client.SendAsync(Request(grpcPort, HttpVersion.Version11));

        // Assert
        (await http1.Content.ReadAsStringAsync()).ShouldBe("HTTP/1.1");
        (await h2c.Content.ReadAsStringAsync()).ShouldBe("HTTP/2");
        // Kestrel rejects the request before the app: "An HTTP/1.x request was sent to an HTTP/2 only endpoint."
        http1OnGrpcPort.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await app.StopAsync();
    }

    private static HttpRequestMessage Request(int port, Version version) => new(HttpMethod.Get, $"http://127.0.0.1:{port}/")
    {
        Version = version,
        VersionPolicy = HttpVersionPolicy.RequestVersionExact,
    };
}
