namespace FullstackTemplate.Bff.Tests.Integration;

using System.Net;
using Shouldly;

/// <summary>
/// The refresh failure rules of section 3.7 of the spec: <c>invalid_grant</c> ends the BFF session, and a
/// temporary failure keeps it.
/// </summary>
public class RefreshFailureTests
{
    [Test]
    public async Task an_invalid_grant_ends_the_session_and_the_request_goes_with_no_token()
    {
        var app = BffApp.Instance;
        var provider = app.IdentityProvider;
        var subject = NewSubject();
        using var browser = app.CreateBrowser();
        var key = app.SessionKeyIn(await BffApp.SignInAsync(browser, providerQuery: $"test_sub={subject}&test_expires_in=60&test_refresh=fail"))!;

        var response = await browser.GetApiAsync("/api/v1/items");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        provider.RefreshCount(subject).ShouldBe(1);
        (await response.ReadEchoAsync()).Headers.ShouldNotContainKey("Authorization");
        BffApp.SessionCookieValue(response).ShouldBe("");
        (await app.WithStoreAsync(store => store.GetAsync(key, CancellationToken.None))).ShouldBeNull();
        (await browser.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task a_503_while_the_token_is_valid_sends_the_current_token_and_keeps_the_session()
    {
        var app = BffApp.Instance;
        var provider = app.IdentityProvider;
        var subject = NewSubject();
        using var browser = app.CreateBrowser();
        var key = app.SessionKeyIn(await BffApp.SignInAsync(browser, providerQuery: $"test_sub={subject}&test_expires_in=60&test_refresh=unavailable"))!;

        var response = await browser.GetApiAsync("/api/v1/items");

        provider.RefreshCount(subject).ShouldBe(1);
        (await response.ReadEchoAsync()).Headers["Authorization"].ShouldBe($"Bearer {provider.IssuedAccessTokens(subject).Single()}");
        (await app.WithStoreAsync(store => store.GetAsync(key, CancellationToken.None))).ShouldNotBeNull();
        (await browser.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task a_503_after_the_token_expired_sends_no_token_keeps_the_session_and_the_next_call_tries_again()
    {
        var app = BffApp.Instance;
        var provider = app.IdentityProvider;
        var subject = NewSubject();
        using var browser = app.CreateBrowser();
        // expires_in 0: the access token has expired when the login ends
        var key = app.SessionKeyIn(await BffApp.SignInAsync(browser, providerQuery: $"test_sub={subject}&test_expires_in=0&test_refresh=unavailable"))!;

        var response = await browser.GetApiAsync("/api/v1/items");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadEchoAsync()).Headers.ShouldNotContainKey("Authorization");
        (await app.WithStoreAsync(store => store.GetAsync(key, CancellationToken.None))).ShouldNotBeNull();
        (await browser.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.OK);

        await browser.GetApiAsync("/api/v1/items");
        provider.RefreshCount(subject).ShouldBe(2);
    }

    private static string NewSubject() => $"user-refresh-failure-{Guid.NewGuid():N}";
}
