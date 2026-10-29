namespace FullstackTemplate.Bff.Tests.Integration;

using System.Net;
using Shouldly;

public class CsrfHeaderTests
{
    [Test]
    [Arguments("GET", "/bff/user")]
    [Arguments("GET", "/local-api/hello")]
    [Arguments("GET", "/api/v1/items")]
    [Arguments("POST", "/api/v1/items")]
    [Arguments("PUT", "/api/v1/items/1")]
    [Arguments("PATCH", "/api/v1/items/1")]
    [Arguments("DELETE", "/api/v1/items/1")]
    [Arguments("HEAD", "/api/v1/items")]
    [Arguments("OPTIONS", "/api/v1/items")]
    [Arguments("GET", "/api")]
    [Arguments("GET", "/API/v1/items")]
    public async Task a_bff_api_request_without_the_header_gets_401_with_an_empty_body(string method, string path)
    {
        // Arrange
        using var browser = BffApp.Instance.CreateBrowser();

        // Act
        var response = await browser.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        // Assert
        await ShouldBeRejectedAsync(response);
    }

    [Test]
    [Arguments("0")]
    [Arguments("true")]
    [Arguments(" 1")]
    [Arguments("")]
    public async Task a_header_value_other_than_1_gets_401(string value)
    {
        // Arrange
        using var browser = BffApp.Instance.CreateBrowser();
        var request = new HttpRequestMessage(HttpMethod.Get, "/local-api/hello");
        request.Headers.TryAddWithoutValidation("X-CSRF", value);

        // Act
        var response = await browser.SendAsync(request);

        // Assert
        await ShouldBeRejectedAsync(response);
    }

    [Test]
    public async Task only_the_first_header_value_counts()
    {
        // Arrange
        using var browser = BffApp.Instance.CreateBrowser();
        var request = new HttpRequestMessage(HttpMethod.Get, "/local-api/hello");
        request.Headers.TryAddWithoutValidation("X-CSRF", ["2", "1"]);

        // Act
        var response = await browser.SendAsync(request);

        // Assert
        await ShouldBeRejectedAsync(response);
    }

    [Test]
    public async Task a_signed_in_request_without_the_header_gets_401()
    {
        // Arrange
        using var browser = BffApp.Instance.CreateBrowser();
        await BffApp.SignInAsync(browser, providerQuery: "test_sub=csrf-user");

        // Act
        var response = await browser.GetAsync("/bff/user");

        // Assert
        await ShouldBeRejectedAsync(response);
    }

    [Test]
    [Arguments("GET", "/local-api/hello")]
    [Arguments("GET", "/api/v1/items")]
    [Arguments("POST", "/api/v1/items")]
    [Arguments("DELETE", "/api/v1/items/1")]
    public async Task a_request_with_the_header_passes(string method, string path)
    {
        // Arrange
        using var browser = BffApp.Instance.CreateBrowser();

        // Act
        var response = await browser.SendApiAsync(new HttpMethod(method), path);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    [Arguments("/bff/login")]
    [Arguments("/bff/logout")]
    public async Task the_login_and_logout_endpoints_need_no_header(string path)
    {
        // Arrange
        using var browser = BffApp.Instance.CreateBrowser();

        // Act
        var response = await browser.GetAsync(path);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    }

    private static async Task ShouldBeRejectedAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).ShouldBeEmpty();
        response.Headers.WwwAuthenticate.ShouldBeEmpty();
    }
}
