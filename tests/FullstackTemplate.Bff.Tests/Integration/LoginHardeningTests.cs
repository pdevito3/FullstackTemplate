namespace FullstackTemplate.Bff.Tests.Integration;

using System.Net;
using FullstackTemplate.Bff.Features;
using FullstackTemplate.Bff.Login;
using FullstackTemplate.Bff.Tests.Fakes;
using Shouldly;

/// <summary>
/// The callback checks and the client authentication, against the default fake provider. It lists the <c>iss</c>
/// parameter and both client authentication methods, as Keycloak does.
/// </summary>
public class LoginHardeningTests
{
    [Test]
    public async Task a_callback_with_the_right_iss_signs_in()
    {
        using var browser = BffApp.Instance.CreateBrowser();

        var callback = await BffApp.SignInAsync(browser, providerQuery: $"test_sub={NewSubject()}");

        callback.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        (await browser.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    [Arguments("test_iss=https%3A%2F%2Fevil.example")]
    [Arguments("test_iss=omit")]
    public async Task a_callback_with_a_wrong_or_missing_iss_gets_400_problem_and_no_session(string issQuery)
    {
        var subject = NewSubject();
        using var browser = BffApp.Instance.CreateBrowser();

        var callback = await BffApp.SignInAsync(browser, providerQuery: $"test_sub={subject}&{issQuery}");

        await LoginEndpointTests.ShouldBeProblemAsync(callback, CallbackRejectedException.Title, "iss");
        BffApp.SessionCookieValue(callback).ShouldBeNull();
        (await browser.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        // The BFF does not redeem the code of a response that it does not trust
        BffApp.Instance.IdentityProvider.TokenRequests.ShouldNotContain(r => r.Subject == subject);
    }

    [Test]
    public async Task an_error_response_with_a_wrong_iss_gets_400_and_not_the_prompt_redirect()
    {
        using var browser = BffApp.Instance.CreateBrowser();

        var callback = await BffApp.SignInAsync(browser, "?prompt=none&returnUrl=%2Fback",
            "test_error=login_required&test_iss=https%3A%2F%2Fevil.example");

        await LoginEndpointTests.ShouldBeProblemAsync(callback, CallbackRejectedException.Title, "iss");
    }

    [Test]
    public async Task an_error_response_with_the_right_iss_still_redirects_a_prompt_login()
    {
        using var browser = BffApp.Instance.CreateBrowser();

        var callback = await BffApp.SignInAsync(browser, "?prompt=none&returnUrl=%2Fback", "test_error=login_required");

        callback.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        callback.Headers.Location!.OriginalString.ShouldBe($"{BffApp.SpaDevServer}/back");
    }

    [Test]
    public async Task a_callback_with_an_invalid_state_gets_400_problem_and_no_session()
    {
        using var browser = BffApp.Instance.CreateBrowser();
        var issuer = Uri.EscapeDataString(BffApp.Instance.IdentityProvider.Issuer);

        var callback = await browser.GetAsync($"/signin-oidc?code=x&state=not-a-state&iss={issuer}");

        await LoginEndpointTests.ShouldBeProblemAsync(callback, CallbackRejectedException.Title, CallbackRejection.CallbackField);
        BffApp.SessionCookieValue(callback).ShouldBeNull();
    }

    [Test]
    public async Task an_error_response_without_a_prompt_gets_400_problem()
    {
        using var browser = BffApp.Instance.CreateBrowser();

        var callback = await BffApp.SignInAsync(browser, providerQuery: "test_error=access_denied");

        await LoginEndpointTests.ShouldBeProblemAsync(callback, CallbackRejectedException.Title, CallbackRejection.CallbackField);
        BffApp.SessionCookieValue(callback).ShouldBeNull();
    }

    [Test]
    public async Task an_unsigned_id_token_gets_400_problem_and_no_session()
    {
        using var browser = BffApp.Instance.CreateBrowser();

        var callback = await BffApp.SignInAsync(browser, providerQuery: $"test_sub={NewSubject()}&test_id_token=unsigned");

        await LoginEndpointTests.ShouldBeProblemAsync(callback, CallbackRejectedException.Title, IdTokenChecks.AlgorithmHeader);
        BffApp.SessionCookieValue(callback).ShouldBeNull();
        (await browser.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task an_id_token_with_another_audience_gets_400_problem_and_no_session()
    {
        using var browser = BffApp.Instance.CreateBrowser();

        var callback = await BffApp.SignInAsync(browser, providerQuery: $"test_sub={NewSubject()}&test_aud=other-api");

        await LoginEndpointTests.ShouldBeProblemAsync(callback, CallbackRejectedException.Title, IdTokenChecks.AudienceClaim);
        BffApp.SessionCookieValue(callback).ShouldBeNull();
        (await browser.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// The custom code redemption makes the handler skip its checks of the token response, so
    /// <see cref="IdTokenChecks"/> runs them: the nonce, at_hash, and azp (Core 3.1.3.7).
    /// </summary>
    [Test]
    [Arguments("test_nonce=wrong")]
    [Arguments("test_at_hash=wrong")]
    [Arguments("test_azp=other-client")]
    public async Task an_id_token_that_fails_a_token_response_check_gets_400_problem_and_no_session(string providerQuery)
    {
        using var browser = BffApp.Instance.CreateBrowser();

        var callback = await BffApp.SignInAsync(browser, providerQuery: $"test_sub={NewSubject()}&{providerQuery}");

        await LoginEndpointTests.ShouldBeProblemAsync(callback, CallbackRejectedException.Title, CallbackRejection.CallbackField);
        BffApp.SessionCookieValue(callback).ShouldBeNull();
        (await browser.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// A valid authorization response, in the query and in a form body. Without the guard, the handler reads the
    /// form of a POST to <c>/signin-oidc</c> and signs in. The handler also fails the other cases without the guard
    /// (no form, no correlation cookie on the upper-case path, or not a POST), so they only keep the 400.
    /// </summary>
    [Test]
    [MethodDataSource(nameof(CallbackMethods))]
    public async Task a_callback_that_is_not_a_get_gets_400_and_the_code_is_not_redeemed(HttpMethod method, string path, bool withForm)
    {
        var subject = NewSubject();
        using var browser = BffApp.Instance.CreateBrowser();
        var login = await browser.GetAsync("/bff/login");
        var callback = await BffApp.AuthorizeAsync(login.Headers.Location!, $"test_sub={subject}");
        var parameters = BffApp.Query(callback);
        var form = withForm
            ? new FormUrlEncodedContent(parameters.AllKeys.Select(k => new KeyValuePair<string, string>(k!, parameters[k]!)))
            : null;

        var response = await browser.SendAsync(new HttpRequestMessage(method, path + callback.Query) { Content = form });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        BffApp.SessionCookieValue(response).ShouldBeNull();
        BffApp.Instance.IdentityProvider.TokenRequests.ShouldNotContain(r => r.Subject == subject);
    }

    public static IEnumerable<Func<(HttpMethod, string, bool)>> CallbackMethods()
    {
        yield return () => (HttpMethod.Post, "/signin-oidc", true);
        yield return () => (HttpMethod.Post, "/SIGNIN-OIDC", true);
        yield return () => (HttpMethod.Post, "/signin-oidc", false);
        yield return () => (HttpMethod.Put, "/signin-oidc", true);
        yield return () => (HttpMethod.Head, "/signin-oidc", false);
    }

    [Test]
    public async Task the_code_exchange_the_refresh_and_the_revocation_use_client_secret_basic()
    {
        var subject = NewSubject();
        var provider = BffApp.Instance.IdentityProvider;
        using var browser = BffApp.Instance.CreateBrowser();
        // A token that expires within the refresh window, so that the next API call refreshes it
        await BffApp.SignInAsync(browser, providerQuery: $"test_sub={subject}&test_expires_in=60");

        (await browser.GetApiAsync("/api/v1/weather")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await browser.GetAsync("/bff/logout")).StatusCode.ShouldBe(HttpStatusCode.Redirect);

        provider.TokenRequests.Where(r => r.Subject == subject).Select(r => (r.GrantType, r.ClientAuthentication)).ShouldBe(
        [
            ("authorization_code", "client_secret_basic"),
            ("refresh_token", "client_secret_basic"),
        ]);
        provider.RevocationClientAuthentications(subject).ShouldBe(["client_secret_basic"]);
    }

    [Test]
    public async Task client_authentication_post_sends_the_secret_in_the_body()
    {
        await using var app = BffApp.StartAnotherInstance(new Dictionary<string, string?>
        {
            ["Auth:ClientAuthentication"] = nameof(ClientAuthenticationMethod.Post),
        });
        var subject = NewSubject();
        using var browser = app.CreateBrowser();

        await BffApp.SignInAsync(browser, providerQuery: $"test_sub={subject}&test_expires_in=60");
        (await browser.GetApiAsync("/api/v1/weather")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await browser.GetAsync("/bff/logout")).StatusCode.ShouldBe(HttpStatusCode.Redirect);

        app.IdentityProvider.RevocationClientAuthentications(subject).ShouldBe(["client_secret_post"]);
        app.IdentityProvider.TokenRequests.Where(r => r.Subject == subject).Select(r => (r.GrantType, r.ClientAuthentication)).ShouldBe(
        [
            ("authorization_code", "client_secret_post"),
            ("refresh_token", "client_secret_post"),
        ]);
    }

    [Test]
    public async Task a_get_to_the_front_channel_logout_path_does_not_end_the_session()
    {
        using var browser = BffApp.Instance.CreateBrowser();
        await BffApp.SignInAsync(browser, providerQuery: $"test_sub={NewSubject()}&test_sid=sid-front");

        var path = "/signout-oidc?sid=sid-front&iss=" + Uri.EscapeDataString(BffApp.Instance.IdentityProvider.Issuer);
        var response = await browser.GetAsync(path);

        // No endpoint answers the path, so in a local run it goes to the SPA dev server like any unknown path
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.ShouldBe(BffApp.SpaDevServer + path);
        BffApp.SessionCookieValue(response).ShouldBeNull();
        (await browser.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task back_channel_logout_disable_rejects_each_logout_token_and_ends_no_session()
    {
        await using var app = BffApp.StartAnotherInstance(new Dictionary<string, string?>
        {
            ["Auth:Features:BackChannelLogout"] = nameof(FeatureMode.Disable),
        });
        var subject = NewSubject();
        using var browser = app.CreateBrowser();
        await BffApp.SignInAsync(browser, providerQuery: $"test_sub={subject}");

        var response = await browser.PostAsync("/bff/backchannel", new FormUrlEncodedContent(
            [new("logout_token", BackChannelLogoutTests.Token(c => { c["sub"] = subject; c.Remove("sid"); }))]));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await browser.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task a_required_feature_that_discovery_does_not_list_stops_the_bff_at_startup()
    {
        var exception = Should.Throw<Exception>(() => BffApp.StartAnotherInstance(new Dictionary<string, string?>
        {
            ["Auth:Features:PushedAuthorization"] = nameof(FeatureMode.Require),
        }));

        exception.ShouldBeOfType<ProviderFeatureException>();
        exception.Message.ShouldContain("pushed_authorization_request_endpoint");
    }

    [Test]
    public async Task a_failed_discovery_read_stops_the_bff_at_startup()
    {
        var gone = await FakeIdentityProvider.StartAsync();
        await gone.DisposeAsync();

        var exception = Should.Throw<Exception>(() => BffApp.StartAnotherInstance(identityProvider: gone));

        exception.ShouldBeOfType<InvalidOperationException>();
        exception.Message.ShouldContain(gone.Issuer);
    }

    private static string NewSubject() => $"user-{Guid.NewGuid():N}";
}
