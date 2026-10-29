namespace FullstackTemplate.Bff.Tests.Integration;

using System.Net;
using FullstackTemplate.Bff.Features;
using FullstackTemplate.Bff.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

/// <summary>
/// The features that the BFF decides from discovery, against a provider with PAR and against a provider with few
/// features, as Authentik and FusionAuth are.
/// </summary>
public class ProviderFeatureTests
{
    private static FakeIdentityProvider _parProvider = null!;
    private static FakeIdentityProvider _minimalProvider = null!;
    private static BffApp _parBff = null!;
    private static BffApp _minimalBff = null!;

    [Before(Class)]
    public static async Task StartAsync()
    {
        _parProvider = await FakeIdentityProvider.StartAsync(new FakeProviderOptions { PushedAuthorization = true });
        _minimalProvider = await FakeIdentityProvider.StartAsync(new FakeProviderOptions
        {
            IssuerParameter = false,
            EndSession = false,
            Revocation = false,
            BackChannelLogout = false,
            TokenEndpointAuthMethods = ["client_secret_post"],
        });
        _parBff = BffApp.StartAnotherInstance(identityProvider: _parProvider);
        _minimalBff = BffApp.StartAnotherInstance(identityProvider: _minimalProvider);
    }

    [After(Class)]
    public static async Task StopAsync()
    {
        await _parBff.DisposeAsync();
        await _minimalBff.DisposeAsync();
        await _parProvider.DisposeAsync();
        await _minimalProvider.DisposeAsync();
    }

    [Test]
    public async Task with_par_the_authorize_url_has_only_client_id_and_request_uri_and_the_login_finishes()
    {
        // Arrange
        using var browser = _parBff.CreateBrowser();

        // Act
        var login = await browser.GetAsync("/bff/login?returnUrl=%2Fweather");

        // Assert
        login.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var authorize = login.Headers.Location!;
        authorize.GetLeftPart(UriPartial.Path).ShouldBe($"{_parProvider.Issuer}/authorize");
        var query = BffApp.Query(authorize);
        query.AllKeys.Order().ShouldBe(["client_id", "request_uri"]);
        query["client_id"].ShouldBe(FakeIdentityProvider.ClientId);
        // RFC 9126 section 2: the PAR request uses the client authentication of the token endpoint
        _parProvider.PushedAuthorizationRequests.Single(r => r.RequestUri == query["request_uri"])
            .ClientAuthentication.ShouldBe("client_secret_basic");

        var callback = await browser.GetAsync((await BffApp.AuthorizeAsync(authorize, $"test_sub={NewSubject()}")).PathAndQuery);
        callback.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        callback.Headers.Location!.OriginalString.ShouldBe($"{BffApp.SpaDevServer}/weather");
        (await browser.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task with_par_and_client_authentication_post_the_par_request_sends_the_secret_in_the_body()
    {
        // Arrange
        await using var app = BffApp.StartAnotherInstance(new Dictionary<string, string?>
        {
            ["Auth:ClientAuthentication"] = nameof(ClientAuthenticationMethod.Post),
        }, identityProvider: _parProvider);
        var subject = NewSubject();
        using var browser = app.CreateBrowser();

        // Act
        var login = await browser.GetAsync("/bff/login");

        // Assert
        var requestUri = BffApp.Query(login.Headers.Location!)["request_uri"];
        requestUri.ShouldNotBeNull();
        _parProvider.PushedAuthorizationRequests.Single(r => r.RequestUri == requestUri)
            .ClientAuthentication.ShouldBe("client_secret_post");
        var callback = await browser.GetAsync((await BffApp.AuthorizeAsync(login.Headers.Location!, $"test_sub={subject}")).PathAndQuery);
        callback.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        _parProvider.TokenRequests.Single(r => r.Subject == subject).ClientAuthentication.ShouldBe("client_secret_post");
    }

    [Test]
    public async Task with_par_disabled_the_login_uses_the_plain_authorize_url()
    {
        // Arrange
        await using var app = BffApp.StartAnotherInstance(new Dictionary<string, string?>
        {
            ["Auth:Features:PushedAuthorization"] = nameof(FeatureMode.Disable),
        }, identityProvider: _parProvider);
        using var browser = app.CreateBrowser();

        // Act
        var login = await browser.GetAsync("/bff/login");

        // Assert
        var query = BffApp.Query(login.Headers.Location!);
        query["request_uri"].ShouldBeNull();
        query["response_type"].ShouldBe("code");
        query["code_challenge_method"].ShouldBe("S256");
    }

    [Test]
    public async Task without_par_the_login_uses_the_plain_authorize_url()
    {
        // Arrange
        using var browser = _minimalBff.CreateBrowser();

        // Act
        var login = await browser.GetAsync("/bff/login");

        // Assert
        var query = BffApp.Query(login.Headers.Location!);
        query["request_uri"].ShouldBeNull();
        query["response_type"].ShouldBe("code");
    }

    [Test]
    public async Task without_the_iss_parameter_a_callback_with_no_iss_signs_in()
    {
        // Arrange
        using var browser = _minimalBff.CreateBrowser();

        // Act
        var callback = await BffApp.SignInAsync(browser, providerQuery: $"test_sub={NewSubject()}");

        // Assert
        callback.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        (await browser.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task without_the_iss_parameter_a_present_iss_must_still_match()
    {
        // Arrange
        using var browser = _minimalBff.CreateBrowser();

        // Act
        var callback = await BffApp.SignInAsync(browser, providerQuery: $"test_sub={NewSubject()}&test_iss=https%3A%2F%2Fevil.example");

        // Assert
        await LoginEndpointTests.ShouldBeProblemAsync(callback, Login.CallbackRejectedException.Title, "iss");
    }

    [Test]
    public async Task a_provider_that_lists_only_client_secret_post_gets_it_for_the_code_exchange_and_the_refresh()
    {
        // Arrange
        var subject = NewSubject();
        using var browser = _minimalBff.CreateBrowser();

        // Act
        await BffApp.SignInAsync(browser, providerQuery: $"test_sub={subject}&test_expires_in=60");

        // Assert
        (await browser.GetApiAsync("/api/v1/weather")).StatusCode.ShouldBe(HttpStatusCode.OK);

        _minimalProvider.TokenRequests.Where(r => r.Subject == subject).Select(r => (r.GrantType, r.ClientAuthentication)).ShouldBe(
        [
            ("authorization_code", "client_secret_post"),
            ("refresh_token", "client_secret_post"),
        ]);
    }

    [Test]
    public async Task without_end_session_and_revocation_logout_ends_the_session_and_redirects_to_the_local_return_url()
    {
        // Arrange
        var subject = NewSubject();
        using var browser = _minimalBff.CreateBrowser();
        var key = _minimalBff.SessionKeyIn(await BffApp.SignInAsync(browser, providerQuery: $"test_sub={subject}"))!;

        // Act
        var logout = await browser.GetAsync("/bff/logout?returnUrl=%2Fbye");

        // Assert
        logout.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        logout.Headers.Location!.OriginalString.ShouldBe($"{BffApp.SpaDevServer}/bye");
        logout.Headers.GetValues("Set-Cookie").ShouldContain(c => c.StartsWith("__Host-bff=;", StringComparison.Ordinal));
        (await _minimalBff.WithStoreAsync(store => store.GetAsync(key, CancellationToken.None))).ShouldBeNull();
        (await browser.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        _minimalProvider.RevokedTokens.ShouldBeEmpty();
    }

    [Test]
    public async Task with_a_logout_endpoint_setting_logout_sends_client_id_and_logout_uri_and_the_callback_returns_to_the_app()
    {
        // Arrange
        // Cognito lists no end_session_endpoint. Its logout endpoint reads client_id and logout_uri, and it sends no state back.
        var logoutEndpoint = $"{_minimalProvider.Issuer}/logout";
        await using var app = BffApp.StartAnotherInstance(new Dictionary<string, string?>
        {
            ["Auth:LogoutEndpoint"] = logoutEndpoint,
        }, identityProvider: _minimalProvider);
        using var browser = app.CreateBrowser();
        await BffApp.SignInAsync(browser, providerQuery: $"test_sub={NewSubject()}");

        // Act
        var logout = await browser.GetAsync("/bff/logout");

        // Assert
        logout.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = logout.Headers.Location!;
        location.GetLeftPart(UriPartial.Path).ShouldBe(logoutEndpoint);
        var query = BffApp.Query(location);
        query["client_id"].ShouldBe(FakeIdentityProvider.ClientId);
        query["logout_uri"].ShouldBe("https://localhost/signout-callback-oidc");

        var callback = await browser.GetAsync("/signout-callback-oidc");

        callback.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        callback.Headers.Location!.OriginalString.ShouldBe("/");
    }

    [Test]
    public async Task each_bff_decides_its_features_from_the_discovery_of_its_provider()
    {
        // Act & Assert
        _parBff.Services.GetRequiredService<ProviderFeatures>().Active.ShouldBe(new ActiveFeatures(
            _parProvider.Issuer, Revocation: true, IssuerParameterRule.Required, BackChannelLogout: true, EndSession: true,
            PushedAuthorization: true, ClientAuthenticationMethod.Basic));
        _minimalBff.Services.GetRequiredService<ProviderFeatures>().Active.ShouldBe(new ActiveFeatures(
            _minimalProvider.Issuer, Revocation: false, IssuerParameterRule.IfPresent, BackChannelLogout: true, EndSession: false,
            PushedAuthorization: false, ClientAuthenticationMethod.Post));
    }

    private static string NewSubject() => $"user-{Guid.NewGuid():N}";
}
