namespace FullstackTemplate.Bff.Tests.Integration;

using System.Net;
using Shouldly;

public class LogoutEndpointTests
{
    [Test]
    [Arguments("")]
    [Arguments("?sid=other")]
    [Arguments("?sid=SID-1")]
    public async Task a_session_with_a_sid_needs_the_same_sid(string query)
    {
        using var browser = BffApp.Instance.CreateBrowser();
        await BffApp.SignInAsync(browser, providerQuery: "test_sub=user-sid-check&test_sid=sid-1");

        var response = await browser.GetAsync($"/bff/logout{query}");

        await LoginEndpointTests.ShouldBeProblemAsync(response, "Invalid Session ID", "sid");
        (await browser.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task logout_with_the_sid_revokes_the_refresh_token_ends_the_session_and_redirects_to_end_session()
    {
        var provider = BffApp.Instance.IdentityProvider;
        using var browser = BffApp.Instance.CreateBrowser();
        await BffApp.SignInAsync(browser, providerQuery: "test_sub=user-logout&test_sid=sid-2");

        var response = await browser.GetAsync("/bff/logout?sid=sid-2");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = response.Headers.Location!;
        location.GetLeftPart(UriPartial.Path).ShouldBe($"{provider.Issuer}/endsession");
        var query = BffApp.Query(location);
        query["id_token_hint"].ShouldNotBeNullOrEmpty();
        query["post_logout_redirect_uri"].ShouldBe("https://localhost/signout-callback-oidc");

        provider.WasRefreshTokenRevoked("user-logout").ShouldBeTrue();
        response.Headers.GetValues("Set-Cookie").ShouldContain(c => c.StartsWith("__Host-bff=;", StringComparison.Ordinal));
        (await browser.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task a_session_with_no_sid_needs_no_sid()
    {
        using var browser = BffApp.Instance.CreateBrowser();
        await BffApp.SignInAsync(browser, providerQuery: "test_sub=user-logout-no-sid");

        var response = await browser.GetAsync("/bff/logout");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.GetLeftPart(UriPartial.Path).ShouldBe($"{BffApp.Instance.IdentityProvider.Issuer}/endsession");
    }

    [Test]
    public async Task an_anonymous_logout_needs_no_sid()
    {
        using var browser = BffApp.Instance.CreateBrowser();

        var response = await browser.GetAsync("/bff/logout");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.GetLeftPart(UriPartial.Path).ShouldBe($"{BffApp.Instance.IdentityProvider.Issuer}/endsession");
    }

    [Test]
    [Arguments("https%3A%2F%2Fevil.example")]
    [Arguments("%2F%2Fevil.example")]
    public async Task a_return_url_that_is_not_local_gets_400_problem(string returnUrl)
    {
        using var browser = BffApp.Instance.CreateBrowser();

        var response = await browser.GetAsync($"/bff/logout?returnUrl={returnUrl}");

        await LoginEndpointTests.ShouldBeProblemAsync(response, "Invalid return url", "returnUrl");
    }

    [Test]
    public async Task the_signed_out_callback_redirects_to_the_return_url()
    {
        using var browser = BffApp.Instance.CreateBrowser();
        var logout = await browser.GetAsync("/bff/logout?returnUrl=%2Fbye");
        var state = BffApp.Query(logout.Headers.Location!)["state"];

        var callback = await browser.GetAsync($"/signout-callback-oidc?state={Uri.EscapeDataString(state!)}");

        callback.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        callback.Headers.Location!.OriginalString.ShouldBe("/bye");
    }
}
