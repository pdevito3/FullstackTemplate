namespace FullstackTemplate.Bff.Tests.Integration;

using System.Net;
using System.Text.RegularExpressions;
using FullstackTemplate.Bff.Management;
using FullstackTemplate.Bff.Proxy;
using FullstackTemplate.Bff.Tests.Fakes;
using Microsoft.Net.Http.Headers;
using Shouldly;

public class JobsDashboardProxyTests
{
    [Test]
    public async Task a_browser_with_no_session_gets_the_login_and_then_comes_back_to_the_dashboard_page_on_the_origin_of_the_spa()
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
        callback.Headers.Location!.OriginalString.ShouldBe(BffApp.SpaDevServer + path);
        var echo = await (await browser.GetAsync(path)).ReadEchoAsync();
        echo.Path.ShouldBe(path);
        echo.Headers["Authorization"].ShouldStartWith("Bearer at-");
    }

    [Test]
    public async Task a_cross_site_navigation_with_no_session_cookie_gets_a_page_that_loads_the_url_again_from_its_own_origin()
    {
        // Arrange
        // The redirect from the identity provider after the login, and a link in an email, are cross-site. A browser
        // does not send the SameSite=Strict session cookie on them, so the request has no session.
        using var browser = BffApp.Instance.CreateBrowser();
        var path = $"{JobsDashboardProxy.Prefix}/jobs?state=DeadLettered&tk=tenant&tv={Guid.NewGuid():N}";
        using var request = Navigation(HttpMethod.Get, path, site: "cross-site", destination: "document");

        // Act
        var response = await browser.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/html");
        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
        var html = await response.Content.ReadAsStringAsync();
        var loadAgain = $"{path}&{JobsDashboardLogin.LoadedAgainParameter}=1";
        var refresh = Regex.Match(html, """<meta http-equiv="refresh" content="0; url=([^"]*)">""");
        WebUtility.HtmlDecode(refresh.Groups[1].Value).ShouldBe(loadAgain);
        var link = Regex.Match(html, """<a href="([^"]*)">""");
        WebUtility.HtmlDecode(link.Groups[1].Value).ShouldBe(loadAgain);
        BffApp.Instance.Api.Requests.ShouldNotContain(r => r.Path == $"{JobsDashboardProxy.Prefix}/jobs");
    }

    [Test]
    public async Task a_load_again_with_no_session_gets_the_login_which_comes_back_to_the_url_with_no_marker()
    {
        // Arrange
        // The browser sent no session cookie on the load again either, so a second page would load again and again
        using var browser = BffApp.Instance.CreateBrowser();
        var path = $"{JobsDashboardProxy.Prefix}/jobs?state=DeadLettered&tv={Guid.NewGuid():N}";
        var loadAgain = $"{path}&{JobsDashboardLogin.LoadedAgainParameter}=1";
        using var request = Navigation(HttpMethod.Get, loadAgain, site: "cross-site", destination: "document");

        // Act
        var challenge = await browser.SendAsync(request);

        // Assert
        challenge.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        challenge.Headers.Location!.GetLeftPart(UriPartial.Path).ShouldBe($"{BffApp.Instance.IdentityProvider.Issuer}/authorize");
        var callbackUrl = await BffApp.AuthorizeAsync(challenge.Headers.Location!, "test_sub=user-dashboard-load-again");
        var callback = await browser.GetAsync(callbackUrl.PathAndQuery);
        callback.Headers.Location!.OriginalString.ShouldBe(BffApp.SpaDevServer + path);
        var echo = await (await browser.GetAsync(loadAgain)).ReadEchoAsync();
        echo.Path.ShouldBe($"{JobsDashboardProxy.Prefix}/jobs");
        echo.Query.ShouldBe(new Uri(BffApp.Origin, path).Query);
    }

    [Test]
    [Arguments("GET", "same-origin", "document")]
    [Arguments("GET", "none", "document")]
    [Arguments("GET", "cross-site", "iframe")]
    [Arguments("POST", "cross-site", "document")]
    public async Task a_request_with_no_session_that_is_not_a_cross_site_navigation_of_a_page_gets_the_login(string method, string site, string destination)
    {
        // Arrange
        // The browser sent the cookie if it had one, so a load again would not give a session
        using var browser = BffApp.Instance.CreateBrowser();
        using var request = Navigation(new HttpMethod(method), $"{JobsDashboardProxy.Prefix}/queues", site, destination);

        // Act
        var response = await browser.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.GetLeftPart(UriPartial.Path).ShouldBe($"{BffApp.Instance.IdentityProvider.Issuer}/authorize");
    }

    [Test]
    public async Task a_login_for_a_dashboard_url_longer_than_the_return_url_limit_comes_back_to_the_dashboard_root()
    {
        // Arrange
        using var browser = BffApp.Instance.CreateBrowser();
        var path = $"{JobsDashboardProxy.Prefix}/jobs?wire={new string('w', ReturnUrl.MaxLength)}";
        var challenge = await browser.GetAsync(path);
        var callbackUrl = await BffApp.AuthorizeAsync(challenge.Headers.Location!, "test_sub=user-dashboard-long-url");

        // Act
        var callback = await browser.GetAsync(callbackUrl.PathAndQuery);

        // Assert
        callback.Headers.Location!.OriginalString.ShouldBe(BffApp.SpaDevServer + JobsDashboardProxy.Prefix);
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
    public async Task a_set_cookie_of_the_server_for_the_session_cookie_is_removed_and_the_others_reach_the_browser_with_secure()
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
        // The server got the request over http, so it did not mark the cookie Secure. The origin of the BFF is https.
        var antiforgery = SetCookieHeaderValue.ParseList(cookies).Where(c => c.Name == "backwave-antiforgery").ShouldHaveSingleItem();
        antiforgery.Value.ToString().ShouldBe("from-the-server");
        antiforgery.Path.ToString().ShouldBe(JobsDashboardProxy.Prefix);
        antiforgery.SameSite.ShouldBe(SameSiteMode.Strict);
        antiforgery.HttpOnly.ShouldBeTrue();
        antiforgery.Secure.ShouldBeTrue();
        BffApp.Instance.SessionKeyIn(response).ShouldNotBeNull();
        (await browser.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // The Fetch Metadata headers of a navigation: a page load, a frame load, or a form post
    private static HttpRequestMessage Navigation(HttpMethod method, string path, string site, string destination)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Sec-Fetch-Site", site);
        request.Headers.Add("Sec-Fetch-Mode", "navigate");
        request.Headers.Add("Sec-Fetch-Dest", destination);
        return request;
    }
}
