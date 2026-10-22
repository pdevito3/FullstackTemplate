namespace FullstackTemplate.Bff.Tests.Integration;

using System.Net;
using FullstackTemplate.Bff.Authentication;
using FullstackTemplate.Bff.Tests.Fakes;
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

    [Test]
    public async Task a_slow_revocation_after_a_rejected_id_token_still_ends_the_session()
    {
        await using var provider = await FakeIdentityProvider.StartAsync();
        await using var app = BffApp.StartAnotherInstance(
            new Dictionary<string, string?> { ["Bff:Refresh:TokenRequestTimeout"] = "00:00:00.300" },
            identityProvider: provider);
        var subject = NewSubject();
        using var browser = app.CreateBrowser();
        var key = app.SessionKeyIn(await BffApp.SignInAsync(browser,
            providerQuery: $"test_sub={subject}&test_expires_in=60&test_refresh_id_token=wrong_sub"))!;
        var gate = provider.HoldRevocations();
        try
        {
            var response = await browser.GetApiAsync("/api/v1/weather").WaitAsync(Timeout);

            gate.Arrived.Task.IsCompleted.ShouldBeTrue();
            (await response.ReadEchoAsync()).Headers.ShouldNotContainKey("Authorization");
            BffApp.SessionCookieValue(response).ShouldBe("");
            (await app.WithStoreAsync(store => store.GetAsync(key, CancellationToken.None))).ShouldBeNull();
        }
        finally
        {
            gate.Released.TrySetResult();
        }
    }

    [Test]
    public async Task a_refresh_id_token_with_a_new_key_ends_the_session_when_the_keys_do_not_load_in_time()
    {
        await using var provider = await FakeIdentityProvider.StartAsync();
        await using var app = BffApp.StartAnotherInstance(
            new Dictionary<string, string?>
            {
                ["Bff:Refresh:TokenRequestTimeout"] = "00:00:00.300",
                [IdentityProviderBackchannel.MetadataRefreshIntervalKey] = "00:00:01",
            },
            identityProvider: provider);
        var subject = NewSubject();
        using var browser = app.CreateBrowser();
        var key = app.SessionKeyIn(await BffApp.SignInAsync(browser,
            providerQuery: $"test_sub={subject}&test_expires_in=60&test_refresh_id_token=valid"))!;
        provider.RotateSigningKey();
        // The shortest time between two refreshes of the keys
        await Task.Delay(TimeSpan.FromSeconds(1.5));
        var gate = provider.HoldJwks();
        try
        {
            var response = await browser.GetApiAsync("/api/v1/weather").WaitAsync(Timeout);

            gate.Arrived.Task.IsCompleted.ShouldBeTrue();
            response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            (await response.ReadEchoAsync()).Headers.ShouldNotContainKey("Authorization");
            BffApp.SessionCookieValue(response).ShouldBe("");
            (await app.WithStoreAsync(store => store.GetAsync(key, CancellationToken.None))).ShouldBeNull();
        }
        finally
        {
            gate.Released.TrySetResult();
        }
    }

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static string NewSubject() => $"user-refresh-id-token-{Guid.NewGuid():N}";
}
