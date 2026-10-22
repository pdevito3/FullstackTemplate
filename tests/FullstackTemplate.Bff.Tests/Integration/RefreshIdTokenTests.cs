namespace FullstackTemplate.Bff.Tests.Integration;

using System.Net;
using Shouldly;

/// <summary>
/// The checks of an ID token in a refresh response (OIDC Core 12.2, section 3.7 of the spec). A refresh response
/// with no ID token, or with a valid one, gives the new tokens. An ID token that fails a check ends the BFF
/// session, and the BFF does not use or store the new tokens.
/// </summary>
public class RefreshIdTokenTests
{
    [Test]
    public async Task a_refresh_response_with_no_id_token_gives_the_new_token_and_keeps_the_login_id_token()
    {
        var app = BffApp.Instance;
        var provider = app.IdentityProvider;
        var subject = NewSubject();
        using var browser = app.CreateBrowser();
        var key = app.SessionKeyIn(await BffApp.SignInAsync(browser, providerQuery: $"test_sub={subject}&test_expires_in=60"))!;

        var response = await browser.GetApiAsync("/api/v1/weather");

        (await response.ReadEchoAsync()).Headers["Authorization"].ShouldBe($"Bearer {provider.IssuedAccessTokens(subject)[1]}");
        var tokens = await RefreshLockTests.StoredTokensAsync(app, key);
        tokens.RefreshToken.ShouldBe(provider.IssuedRefreshTokens(subject)[1]);
        tokens.IdToken.ShouldBe(provider.IssuedIdTokens(subject).Single());
    }

    [Test]
    public async Task a_valid_id_token_in_the_refresh_response_gives_the_new_tokens()
    {
        var app = BffApp.Instance;
        var provider = app.IdentityProvider;
        var subject = NewSubject();
        using var browser = app.CreateBrowser();
        var key = app.SessionKeyIn(await BffApp.SignInAsync(browser,
            providerQuery: $"test_sub={subject}&test_expires_in=60&test_refresh_id_token=valid"))!;

        var response = await browser.GetApiAsync("/api/v1/weather");

        (await response.ReadEchoAsync()).Headers["Authorization"].ShouldBe($"Bearer {provider.IssuedAccessTokens(subject)[1]}");
        var tokens = await RefreshLockTests.StoredTokensAsync(app, key);
        tokens.RefreshToken.ShouldBe(provider.IssuedRefreshTokens(subject)[1]);
        tokens.IdToken.ShouldBe(provider.IssuedIdTokens(subject)[1]);
        provider.WasRefreshTokenRevoked(subject).ShouldBeFalse();
        (await browser.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    [Arguments("wrong_iss")]
    [Arguments("wrong_sub")]
    [Arguments("wrong_aud")]
    [Arguments("wrong_auth_time")]
    [Arguments("bad_signature")]
    public async Task an_invalid_id_token_in_the_refresh_response_ends_the_session_and_drops_the_new_tokens(string fault)
    {
        var app = BffApp.Instance;
        var provider = app.IdentityProvider;
        var subject = NewSubject();
        using var browser = app.CreateBrowser();
        var key = app.SessionKeyIn(await BffApp.SignInAsync(browser,
            providerQuery: $"test_sub={subject}&test_expires_in=60&test_refresh_id_token={fault}"))!;

        var response = await browser.GetApiAsync("/api/v1/weather");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        provider.RefreshCount(subject).ShouldBe(1);
        (await response.ReadEchoAsync()).Headers.ShouldNotContainKey("Authorization");
        BffApp.SessionCookieValue(response).ShouldBe("");
        (await app.WithStoreAsync(store => store.GetAsync(key, CancellationToken.None))).ShouldBeNull();
        provider.RevokedTokens.ShouldContain(provider.IssuedRefreshTokens(subject)[1]);
        (await browser.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static string NewSubject() => $"user-refresh-id-token-{Guid.NewGuid():N}";
}
