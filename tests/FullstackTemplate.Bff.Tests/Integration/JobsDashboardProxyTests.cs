namespace FullstackTemplate.Bff.Tests.Integration;

using System.Net;
using FullstackTemplate.Bff.Proxy;
using FullstackTemplate.Bff.Tests.Fakes;
using Shouldly;

public class JobsDashboardProxyTests
{
    [Test]
    public async Task a_browser_with_no_session_gets_the_login_and_then_comes_back_to_the_dashboard_page()
    {
        // Arrange
        using var browser = BffApp.Instance.CreateBrowser();
        var path = $"{JobsDashboardProxy.Prefix}/jobs/{Guid.NewGuid():N}";

        // Act
        var challenge = await browser.GetAsync(path);

        // Assert
        challenge.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        challenge.Headers.Location!.GetLeftPart(UriPartial.Path).ShouldBe($"{BffApp.Instance.IdentityProvider.Issuer}/authorize");
        BffApp.Instance.Api.Requests.ShouldNotContain(r => r.Path == path);

        var callbackUrl = await BffApp.AuthorizeAsync(challenge.Headers.Location!, "test_sub=user-dashboard-login");
        var callback = await browser.GetAsync(callbackUrl.PathAndQuery);
        callback.Headers.Location!.OriginalString.ShouldBe(path);
        var echo = await (await browser.GetAsync(path)).ReadEchoAsync();
        echo.Path.ShouldBe(path);
        echo.Headers["Authorization"].ShouldStartWith("Bearer at-");
    }

    [Test]
    public async Task a_signed_in_request_keeps_its_path_and_its_cookies_and_gets_only_the_bff_token()
    {
        // Arrange
        var cookies = new CookieContainer();
        cookies.Add(new Cookie("other", "1", "/", BffApp.Origin.Host));
        cookies.Add(new Cookie("backwave-antiforgery", "from-the-server", JobsDashboardProxy.Prefix, BffApp.Origin.Host));
        // A chunk of a large session cookie. The cookie handler reads it only when __Host-bff names the chunks.
        cookies.Add(new Cookie("__Host-bffC1", "chunk", "/", BffApp.Origin.Host));
        using var browser = BffApp.Instance.CreateBrowser(cookies);
        await BffApp.SignInAsync(browser, providerQuery: "test_sub=user-dashboard-proxy");
        var request = new HttpRequestMessage(HttpMethod.Get, $"{JobsDashboardProxy.Prefix}/failures?queue=default");
        request.Headers.Add("Authorization", "Bearer from-the-browser");
        request.Headers.Add("DPoP", "proof-from-the-browser");

        // Act
        var response = await browser.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var echo = await response.ReadEchoAsync();
        echo.Path.ShouldBe($"{JobsDashboardProxy.Prefix}/failures");
        echo.Headers["Authorization"].ShouldStartWith("Bearer at-");
        echo.Headers["Authorization"].ShouldNotContain("from-the-browser");
        echo.Headers.ShouldNotContainKey("DPoP");
        var forwarded = echo.Headers["Cookie"].Split("; ");
        forwarded.ShouldBe(["other=1", "backwave-antiforgery=from-the-server"], ignoreOrder: true);
    }

    [Test]
    public async Task the_bare_prefix_goes_to_the_dashboard_root()
    {
        // Arrange
        using var browser = BffApp.Instance.CreateBrowser();
        await BffApp.SignInAsync(browser, providerQuery: "test_sub=user-dashboard-root");

        // Act
        var response = await browser.GetAsync(JobsDashboardProxy.Prefix);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadEchoAsync()).Path.ShouldBe(JobsDashboardProxy.Prefix);
    }

    [Test]
    public async Task a_form_post_with_no_x_csrf_header_reaches_the_dashboard()
    {
        // Arrange
        using var browser = BffApp.Instance.CreateBrowser();
        await BffApp.SignInAsync(browser, providerQuery: "test_sub=user-dashboard-post");
        var path = $"{JobsDashboardProxy.Prefix}/jobs/{Guid.NewGuid()}/requeue";
        using var form = new FormUrlEncodedContent([new("__RequestVerificationToken", "token")]);

        // Act
        var response = await browser.PostAsync(path, form);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var echo = await response.ReadEchoAsync();
        echo.Method.ShouldBe("POST");
        echo.Path.ShouldBe(path);
        echo.Headers["Authorization"].ShouldStartWith("Bearer at-");
    }

    [Test]
    public async Task a_set_cookie_of_the_server_for_the_session_cookie_is_removed_and_the_others_reach_the_browser()
    {
        // Arrange
        using var browser = BffApp.Instance.CreateBrowser();
        // 60 seconds is less than the 120 seconds of RefreshBeforeExpiration, so the call refreshes and renews the cookie
        await BffApp.SignInAsync(browser, providerQuery: "test_sub=user-dashboard-cookie&test_expires_in=60");
        string[] serverCookies =
        [
            "__Host-bff=from-the-server; path=/; secure; httponly",
            "__Host-bffC1=from-the-server; path=/; secure; httponly",
            "backwave-antiforgery=from-the-server; path=/backwave; samesite=strict; httponly",
        ];
        var query = string.Join("&", serverCookies.Select(c => $"{EchoApi.SetCookieParameter}={Uri.EscapeDataString(c)}"));

        // Act
        var response = await browser.GetAsync($"{JobsDashboardProxy.Prefix}/failures?{query}");

        // Assert
        BffApp.Instance.IdentityProvider.RefreshCount("user-dashboard-cookie").ShouldBe(1);
        var cookies = response.Headers.GetValues("Set-Cookie").ToList();
        cookies.ShouldNotContain(c => c.StartsWith("__Host-bff", StringComparison.Ordinal) && c.Contains("from-the-server", StringComparison.Ordinal));
        cookies.ShouldContain(serverCookies[2]);
        BffApp.Instance.SessionKeyIn(response).ShouldNotBeNull();
        (await browser.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
