namespace FullstackTemplate.Bff.Tests.Integration;

using System.Net;
using Shouldly;

/// <summary>
/// The Duende BFF endpoints that the BFF drops on purpose. They get a 404, like any unknown path.
/// </summary>
public class DroppedEndpointTests
{
    [Test]
    [Arguments("/bff/silent-login")]
    [Arguments("/bff/silent-login-callback")]
    [Arguments("/bff/diagnostics")]
    public async Task a_dropped_endpoint_gets_404_also_for_a_signed_in_user(string path)
    {
        // Arrange
        using var browser = BffApp.Instance.CreateBrowser();
        await BffApp.SignInAsync(browser, providerQuery: "test_sub=user-dropped-endpoints");

        // Act & Assert
        (await browser.GetAsync(path)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await browser.GetApiAsync(path)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
