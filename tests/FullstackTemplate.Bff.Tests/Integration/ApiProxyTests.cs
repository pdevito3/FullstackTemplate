namespace FullstackTemplate.Bff.Tests.Integration;

using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Yarp.ReverseProxy.Forwarder;

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

    [Test]
    public async Task the_api_root_goes_to_the_api_root_of_the_server()
    {
        using var browser = BffApp.Instance.CreateBrowser();

        var response = await browser.GetApiAsync("/api");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadEchoAsync()).Path.ShouldBe("/api");
    }

    [Test]
    public async Task an_unreachable_server_gives_502()
    {
        await using var app = BffApp.StartAnotherInstance(new Dictionary<string, string?>
        {
            ["services:server:http:0"] = $"http://127.0.0.1:{ClosedPort()}",
        });
        using var browser = app.CreateBrowser();

        var response = await browser.GetApiAsync("/api/v1/weather");

        response.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
    }

    [Test]
    public async Task a_server_that_does_not_answer_in_time_gives_504()
    {
        await using var app = BffApp.StartAnotherInstance(services: services =>
            services.Replace(ServiceDescriptor.Singleton<IForwarderHttpClientFactory, ShortTimeoutForwarderHttpClientFactory>()));
        using var browser = app.CreateBrowser();

        var response = await browser.GetApiAsync("/api/test/hang").WaitAsync(TimeSpan.FromSeconds(15));

        response.StatusCode.ShouldBe(HttpStatusCode.GatewayTimeout);
    }

    [Test]
    public async Task a_post_that_fails_at_the_server_is_not_sent_again()
    {
        await using var app = BffApp.StartAnotherInstance();
        using var browser = app.CreateBrowser();
        // A pooled connection to the server first, so that the POST goes on a reused connection, the case in which
        // an HTTP client can send a request again after the server closed the connection
        (await browser.GetApiAsync("/api/v1/weather")).StatusCode.ShouldBe(HttpStatusCode.OK);
        var id = Guid.NewGuid().ToString("N");

        var response = await browser.SendApiAsync(HttpMethod.Post, $"/api/test/drop/{id}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        app.Api.DropCount(id).ShouldBe(1);
    }

    // A port with no listener: the OS gives a free port, and the listener stops at once
    private static int ClosedPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    /// <summary>
    /// The forwarder client with a timeout of 300 ms on each request, in place of the activity timeout of 100 s, so
    /// that a test of a hung server does not wait 100 s. The forwarder sees the same cancellation as from its own
    /// activity timeout.
    /// </summary>
    private sealed class ShortTimeoutForwarderHttpClientFactory : ForwarderHttpClientFactory
    {
        protected override HttpMessageHandler WrapHandler(ForwarderHttpClientContext context, HttpMessageHandler handler) =>
            new TimeoutHandler { InnerHandler = base.WrapHandler(context, handler) };

        private sealed class TimeoutHandler : DelegatingHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromMilliseconds(300));
                return await base.SendAsync(request, timeout.Token);
            }
        }
    }
}
