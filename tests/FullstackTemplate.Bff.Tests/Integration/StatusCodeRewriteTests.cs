namespace FullstackTemplate.Bff.Tests.Integration;

using System.Net;
using FullstackTemplate.Bff.Tests.Fakes;
using Shouldly;

/// <summary>
/// A challenge or a forbid in the real pipeline. <see cref="TestAuthorization"/> makes the endpoint require a
/// user or an admin, so that the OIDC handler challenges with a 302 to the provider and the cookie handler
/// forbids with a 302 to its access denied path.
/// </summary>
public class StatusCodeRewriteTests
{
    [Test]
    [Arguments("/local-api/hello")]
    [Arguments("/api/v1/items")]
    public async Task a_challenge_on_a_bff_api_endpoint_gets_401_with_no_location_and_no_cookies(string path)
    {
        // Arrange
        using var browser = BffApp.Instance.CreateBrowser();

        // Act
        var response = await browser.SendAsync(ApiRequest(path, "user"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.Location.ShouldBeNull();
        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
    }

    [Test]
    [Arguments("/local-api/hello")]
    [Arguments("/api/v1/items")]
    public async Task a_forbid_on_a_bff_api_endpoint_gets_403_with_no_location_and_no_cookies(string path)
    {
        // Arrange
        using var browser = BffApp.Instance.CreateBrowser();
        await BffApp.SignInAsync(browser, providerQuery: "test_sub=user-forbid");

        // Act
        var response = await browser.SendAsync(ApiRequest(path, "admin"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Headers.Location.ShouldBeNull();
        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
    }

    [Test]
    public async Task a_challenge_on_another_endpoint_keeps_the_302_to_the_provider()
    {
        // Arrange
        using var browser = BffApp.Instance.CreateBrowser();
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add(TestAuthorization.HeaderName, "user");

        // Act
        var response = await browser.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldStartWith(BffApp.Instance.IdentityProvider.Issuer);
        response.Headers.Contains("Set-Cookie").ShouldBeTrue();
    }

    [Test]
    public async Task a_forbid_on_another_endpoint_keeps_the_302()
    {
        // Arrange
        using var browser = BffApp.Instance.CreateBrowser();
        await BffApp.SignInAsync(browser, providerQuery: "test_sub=user-forbid-page");
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add(TestAuthorization.HeaderName, "admin");

        // Act
        var response = await browser.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.AbsolutePath.ShouldBe("/Account/AccessDenied");
    }

    private static HttpRequestMessage ApiRequest(string path, string require)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-CSRF", "1");
        request.Headers.Add(TestAuthorization.HeaderName, require);
        return request;
    }
}
