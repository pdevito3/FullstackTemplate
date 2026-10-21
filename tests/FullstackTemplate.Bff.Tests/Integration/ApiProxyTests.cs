namespace FullstackTemplate.Bff.Tests.Integration;

using System.Net;
using Shouldly;

public class ApiProxyTests
{
    [Test]
    public async Task an_anonymous_request_goes_to_the_api_path_with_no_token_and_no_cookie()
    {
        using var browser = BffApp.Instance.CreateBrowser();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/weather?city=x");
        request.Headers.Add("X-CSRF", "1");
        request.Headers.Add("Cookie", "other=1");

        var echo = await (await browser.SendAsync(request)).ReadEchoAsync();

        echo.Method.ShouldBe("POST");
        echo.Path.ShouldBe("/api/v1/weather");
        echo.Headers.ShouldNotContainKey("Authorization");
        echo.Headers.ShouldNotContainKey("Cookie");
        echo.Headers["X-CSRF"].ShouldBe("1");
        echo.Headers["X-Forwarded-Proto"].ShouldBe("https");
        echo.Headers["X-Forwarded-Host"].ShouldBe("localhost");
    }

    [Test]
    public async Task a_signed_in_request_gets_the_access_token_and_no_cookie()
    {
        using var browser = BffApp.Instance.CreateBrowser();
        await BffApp.SignInAsync(browser, providerQuery: "test_sub=user-proxy");

        var response = await browser.GetApiAsync("/api/v1/weather");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var echo = await response.ReadEchoAsync();
        echo.Headers["Authorization"].ShouldStartWith("Bearer at-");
        echo.Headers.ShouldNotContainKey("Cookie");
    }

    [Test]
    public async Task a_token_that_expires_soon_is_refreshed_and_the_renewed_cookie_reaches_the_browser()
    {
        var provider = BffApp.Instance.IdentityProvider;
        using var browser = BffApp.Instance.CreateBrowser();
        // 60 seconds is less than the 120 seconds of RefreshBeforeExpiration, so each call refreshes
        await BffApp.SignInAsync(browser, providerQuery: "test_sub=user-refresh&test_expires_in=60");

        var response = await browser.GetApiAsync("/api/v1/weather");

        provider.RefreshCount("user-refresh").ShouldBe(1);
        (await response.ReadEchoAsync()).Headers["Authorization"].ShouldStartWith("Bearer at-");
        response.Headers.GetValues("Set-Cookie").ShouldContain(c => c.StartsWith("__Host-bff=", StringComparison.Ordinal));
    }

    [Test]
    public async Task a_failed_refresh_forwards_with_no_token_and_keeps_the_session()
    {
        var provider = BffApp.Instance.IdentityProvider;
        using var browser = BffApp.Instance.CreateBrowser();
        await BffApp.SignInAsync(browser, providerQuery: "test_sub=user-refresh-fail&test_expires_in=60&test_refresh=fail");

        var response = await browser.GetApiAsync("/api/v1/weather");

        provider.RefreshCount("user-refresh-fail").ShouldBe(1);
        (await response.ReadEchoAsync()).Headers.ShouldNotContainKey("Authorization");
        (await browser.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task the_local_api_answers_with_the_header()
    {
        using var browser = BffApp.Instance.CreateBrowser();

        var response = await browser.GetApiAsync("/local-api/hello");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadJsonAsync()).GetProperty("message").GetString().ShouldBe("Hello from BFF!");
    }
}
