namespace FullstackTemplate.Bff.Tests.Integration;

using System.Net;
using Shouldly;

/// <summary>
/// The public origin, the forwarded headers, and the host filter of section 3.8 of the spec. The default BFF has
/// <c>Bff:PublicOrigin</c> <c>https://localhost</c>.
/// </summary>
public class HostTrustTests
{
    [Test]
    public async Task a_forged_forwarded_host_and_proto_on_login_still_give_the_configured_redirect_uri()
    {
        using var browser = BffApp.Instance.CreateBrowser();
        var request = new HttpRequestMessage(HttpMethod.Get, "/bff/login");
        request.Headers.Add("X-Forwarded-Host", "evil.example");
        request.Headers.Add("X-Forwarded-Proto", "http");

        var response = await browser.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        BffApp.Query(response.Headers.Location!)["redirect_uri"].ShouldBe("https://localhost/signin-oidc");
    }

    [Test]
    public async Task a_forged_forwarded_host_does_not_reach_the_api()
    {
        using var browser = BffApp.Instance.CreateBrowser();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/weather");
        request.Headers.Add("X-CSRF", "1");
        request.Headers.Add("X-Forwarded-Host", "evil.example");

        var echo = await (await browser.SendAsync(request)).ReadEchoAsync();

        echo.Headers["X-Forwarded-Host"].ShouldBe("localhost");
    }

    [Test]
    [Arguments("/bff/login")]
    [Arguments("/bff/user")]
    [Arguments("/api/v1/weather")]
    [Arguments("/signin-oidc")]
    [Arguments("/health")]
    public async Task a_host_that_is_not_on_the_list_gets_400(string path)
    {
        using var browser = BffApp.Instance.CreateBrowser();

        var response = await browser.SendAsync(WithHost(path, "evil.example"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Headers.Location.ShouldBeNull();
        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
    }

    [Test]
    public async Task the_redirect_uri_comes_from_the_public_origin_and_the_additional_hosts_pass_the_filter()
    {
        await using var app = BffApp.StartAnotherInstance(new Dictionary<string, string?>
        {
            ["Bff:PublicOrigin"] = "https://bff.example:8443",
            ["Bff:AdditionalAllowedHosts"] = "localhost; host.docker.internal",
        });
        using var browser = app.CreateBrowser();

        // The request comes in on localhost, an additional host, and the redirect URI still has the public origin
        var login = await browser.GetAsync("/bff/login");
        var fromPublicHost = await browser.SendAsync(WithHost("/bff/login", "bff.example:8443"));
        var fromDockerHost = await browser.SendAsync(WithHost("/health", "host.docker.internal:5000"));
        var fromOtherHost = await browser.SendAsync(WithHost("/health", "other.example"));

        login.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        BffApp.Query(login.Headers.Location!)["redirect_uri"].ShouldBe("https://bff.example:8443/signin-oidc");
        fromPublicHost.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        fromDockerHost.StatusCode.ShouldBe(HttpStatusCode.OK);
        fromOtherHost.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Test]
    [Arguments("")]
    [Arguments("bff.example")]
    [Arguments("ftp://bff.example")]
    [Arguments("https://bff.example/app")]
    [Arguments("https://bff.example/?x=1")]
    public void the_bff_does_not_start_without_a_valid_public_origin(string publicOrigin)
    {
        var exception = Should.Throw<Exception>(() => BffApp.StartAnotherInstance(new Dictionary<string, string?>
        {
            ["Bff:PublicOrigin"] = publicOrigin,
        }));

        exception.ShouldBeOfType<InvalidOperationException>();
        exception.Message.ShouldContain("Bff:PublicOrigin");
    }

    private static HttpRequestMessage WithHost(string path, string host)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-CSRF", "1");
        request.Headers.Host = host;
        return request;
    }
}
