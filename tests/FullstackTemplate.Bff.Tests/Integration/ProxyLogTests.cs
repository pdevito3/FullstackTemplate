namespace FullstackTemplate.Bff.Tests.Integration;

using System.Net;
using Shouldly;

public class ProxyLogTests
{
    [Test]
    public async Task a_proxied_request_outside_development_logs_no_query_value()
    {
        // Arrange
        var path = $"/api/v1/probe-{Guid.NewGuid():N}";
        var filter = $"jane-{Guid.NewGuid():N}";
        await using var app = BffApp.StartAnotherInstance(environment: "Production");
        using var browser = app.CreateBrowser();

        // Act
        var response = await browser.GetApiAsync($"{path}?filters=email%20%40%3D*%20%22{filter}%22");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadEchoAsync()).Path.ShouldBe(path);
        await WaitForRequestLogAsync(path);
        BffApp.Logs.WithPropertyContaining(filter).Select(e => e.MessageTemplate.Text).ShouldBeEmpty();
    }

    // The request log is the last log of a request, and it is written after the response
    private static async Task WaitForRequestLogAsync(string path)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!BffApp.Logs.WithPropertyContaining(path).Any(e => e.Properties.ContainsKey("RequestPath")))
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"No request log for {path}.");
            await Task.Delay(20);
        }
    }
}
