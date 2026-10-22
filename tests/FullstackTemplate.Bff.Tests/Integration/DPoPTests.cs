namespace FullstackTemplate.Bff.Tests.Integration;

using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using FullstackTemplate.Bff.Features;
using FullstackTemplate.Bff.Proxy;
using FullstackTemplate.Bff.Tests.Fakes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Shouldly;

/// <summary>
/// DPoP (RFC 9449) in the BFF, against a fake provider that binds tokens to the key of the login: the authorize
/// request, the code exchange, the refresh, the proxied API call, and the retry after <c>use_dpop_nonce</c>.
/// </summary>
public class DPoPTests
{
    private static DPoPTestKey _key = null!;
    private static FakeIdentityProvider _provider = null!;
    private static FakeIdentityProvider _parProvider = null!;
    private static FakeIdentityProvider _plainProvider = null!;
    private static BffApp _bff = null!;
    private static BffApp _parBff = null!;

    [Before(Class)]
    public static async Task StartAsync()
    {
        _key = DPoPTestKey.Create();
        _provider = await FakeIdentityProvider.StartAsync(new FakeProviderOptions { DPoP = true });
        _parProvider = await FakeIdentityProvider.StartAsync(new FakeProviderOptions { DPoP = true, PushedAuthorization = true });
        _plainProvider = await FakeIdentityProvider.StartAsync();
        _bff = StartBff(FeatureMode.Require, _provider);
        _parBff = StartBff(FeatureMode.Require, _parProvider);
    }

    [After(Class)]
    public static async Task StopAsync()
    {
        await _bff.DisposeAsync();
        await _parBff.DisposeAsync();
        await _provider.DisposeAsync();
        await _parProvider.DisposeAsync();
        await _plainProvider.DisposeAsync();
    }

    [Test]
    public async Task the_authorize_request_has_the_thumbprint_of_the_key()
    {
        using var browser = _bff.CreateBrowser();

        var login = await browser.GetAsync("/bff/login");

        BffApp.Query(login.Headers.Location!)["dpop_jkt"].ShouldBe(_key.Jkt);
    }

    [Test]
    public async Task a_pushed_authorize_request_has_the_thumbprint_of_the_key()
    {
        using var browser = _parBff.CreateBrowser();
        var subject = NewSubject();

        await BffApp.SignInAsync(browser, providerQuery: $"test_sub={subject}");

        // The authorize URL has only client_id and request_uri, so the thumbprint went in the pushed request
        _parProvider.AuthorizeRequests.Any(r => r["client_id"] == FakeIdentityProvider.ClientId && r.GetValueOrDefault("dpop_jkt") == _key.Jkt
            && r.ContainsKey("request_uri")).ShouldBeTrue();
    }

    [Test]
    public async Task the_code_exchange_has_a_proof_of_the_key()
    {
        using var browser = _bff.CreateBrowser();
        var subject = NewSubject();

        var callback = await BffApp.SignInAsync(browser, providerQuery: $"test_sub={subject}");

        // The provider answers invalid_dpop_proof to a bound login with a missing or wrong proof
        callback.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var exchange = _provider.TokenRequests.Single(t => t.GrantType == "authorization_code" && t.Subject == subject);
        exchange.DPoP.ShouldNotBeNull();
        exchange.DPoP.Error.ShouldBeNull();
        exchange.DPoP.Jkt.ShouldBe(_key.Jkt);
        (await browser.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task the_userinfo_call_has_the_dpop_scheme_and_a_proof_for_the_token()
    {
        using var browser = _bff.CreateBrowser();
        var subject = NewSubject();

        var callback = await BffApp.SignInAsync(browser, providerQuery: $"test_sub={subject}");

        // The provider rejects a bound token with the Bearer scheme, or a proof without the right ath, htm, and htu
        callback.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var userInfo = _provider.UserInfoRequests.Single(r => r.Subject == subject);
        userInfo.Scheme.ShouldBe("DPoP");
        userInfo.DPoP.ShouldNotBeNull();
        userInfo.DPoP.Error.ShouldBeNull();
        userInfo.DPoP.Jkt.ShouldBe(_key.Jkt);
    }

    [Test]
    public async Task the_userinfo_call_is_sent_again_with_the_nonce_when_the_provider_asks_for_one()
    {
        const string nonce = "userinfo-nonce";
        await using var provider = await FakeIdentityProvider.StartAsync(new FakeProviderOptions { DPoP = true, UserInfoNonce = nonce });
        await using var app = StartBff(FeatureMode.Require, provider);
        using var browser = app.CreateBrowser();
        var subject = NewSubject();

        var callback = await BffApp.SignInAsync(browser, providerQuery: $"test_sub={subject}");

        callback.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var requests = provider.UserInfoRequests.Where(r => r.Subject == subject).ToList();
        requests.Count.ShouldBe(2);
        requests[0].DPoP!.Nonce.ShouldBeNull();
        requests[1].DPoP!.Error.ShouldBeNull();
        requests[1].DPoP!.Nonce.ShouldBe(nonce);
    }

    [Test]
    public async Task the_userinfo_call_falls_back_to_bearer_when_the_provider_offers_only_bearer_there()
    {
        await using var provider = await FakeIdentityProvider.StartAsync(new FakeProviderOptions { DPoP = true, UserInfoBearerOnly = true });
        await using var app = StartBff(FeatureMode.Require, provider);
        using var browser = app.CreateBrowser();
        var subject = NewSubject();

        var callback = await BffApp.SignInAsync(browser, providerQuery: $"test_sub={subject}");

        callback.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        provider.UserInfoRequests.Where(r => r.Subject == subject).Select(r => r.Scheme).ShouldBe(["DPoP", "Bearer"]);
    }

    [Test]
    public async Task a_refresh_has_a_proof_of_the_key()
    {
        using var browser = _bff.CreateBrowser();
        var subject = NewSubject();
        // 60 seconds is less than the 120 seconds of RefreshBeforeExpiration, so the API call refreshes
        await BffApp.SignInAsync(browser, providerQuery: $"test_sub={subject}&test_expires_in=60");

        var response = await browser.GetApiAsync($"/api/v1/{PathName()}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _provider.RefreshCount(subject).ShouldBe(1);
        var refresh = _provider.TokenRequests.Single(t => t.GrantType == "refresh_token" && t.Subject == subject);
        refresh.DPoP.ShouldNotBeNull();
        refresh.DPoP.Error.ShouldBeNull();
        refresh.DPoP.Jkt.ShouldBe(_key.Jkt);
    }

    [Test]
    public async Task an_api_call_has_the_dpop_scheme_and_a_proof_for_this_request_and_token()
    {
        using var browser = _bff.CreateBrowser();
        var subject = NewSubject();
        await BffApp.SignInAsync(browser, providerQuery: $"test_sub={subject}");
        var token = _provider.IssuedAccessTokens(subject).Single();
        var path = $"/api/v1/{PathName()}";

        var echo = await (await browser.GetApiAsync($"{path}?city=oslo")).ReadEchoAsync();

        echo.Headers["Authorization"].ShouldBe($"DPoP {token}");
        // htu has no query (RFC 9449 section 4.2), htm is the method of the request to the API, ath is the hash of the token
        var proof = DPoPProofVerifier.Verify(echo.Headers["DPoP"], "GET", $"{BffApp.Instance.Api.Url}{path}", token);
        proof.Error.ShouldBeNull();
        proof.Jkt.ShouldBe(_key.Jkt);
    }

    [Test]
    public async Task each_api_call_has_its_own_proof()
    {
        using var browser = _bff.CreateBrowser();
        await BffApp.SignInAsync(browser, providerQuery: $"test_sub={NewSubject()}");
        var path = $"/api/v1/{PathName()}";

        var first = await (await browser.GetApiAsync(path)).ReadEchoAsync();
        var second = await (await browser.GetApiAsync(path)).ReadEchoAsync();

        first.Headers["DPoP"].ShouldNotBe(second.Headers["DPoP"]);
    }

    [Test]
    public async Task a_post_to_the_api_has_a_proof_for_post()
    {
        using var browser = _bff.CreateBrowser();
        await BffApp.SignInAsync(browser, providerQuery: $"test_sub={NewSubject()}");
        var path = $"/api/v1/{PathName()}";

        var echo = await (await browser.SendApiAsync(HttpMethod.Post, path)).ReadEchoAsync();

        DPoPProofVerifier.Verify(echo.Headers["DPoP"], "POST", $"{BffApp.Instance.Api.Url}{path}").Error.ShouldBeNull();
    }

    [Test]
    public async Task a_browser_dpop_header_does_not_reach_the_api_and_the_bff_proof_does()
    {
        using var browser = _bff.CreateBrowser();
        await BffApp.SignInAsync(browser, providerQuery: $"test_sub={NewSubject()}");
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/{PathName()}");
        request.Headers.Add("X-CSRF", "1");
        request.Headers.Add("DPoP", "proof-from-the-browser");

        var echo = await (await browser.SendAsync(request)).ReadEchoAsync();

        echo.Headers["DPoP"].ShouldNotContain("proof-from-the-browser");
        echo.Headers["Authorization"].ShouldStartWith("DPoP at-");
    }

    [Test]
    public async Task the_bff_sends_the_proof_again_with_the_nonce_when_the_api_asks_for_one()
    {
        using var browser = _bff.CreateBrowser();
        await BffApp.SignInAsync(browser, providerQuery: $"test_sub={NewSubject()}");
        var path = $"/api/v1/{PathName()}";
        var nonce = $"nonce-{Guid.NewGuid():N}";

        var response = await browser.GetApiAsync($"{path}?{EchoApi.RequireNonceParameter}={nonce}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var requests = ApiRequests(path);
        requests.Count.ShouldBe(2);
        DPoPProofVerifier.Verify(requests[0].Headers["DPoP"], "GET", $"{BffApp.Instance.Api.Url}{path}").Nonce.ShouldBeNull();
        var retried = DPoPProofVerifier.Verify(requests[1].Headers["DPoP"], "GET", $"{BffApp.Instance.Api.Url}{path}");
        retried.Error.ShouldBeNull();
        retried.Nonce.ShouldBe(nonce);
        requests[1].Headers["DPoP"].ShouldNotBe(requests[0].Headers["DPoP"]);

        // The BFF kept the nonce for the URL, so the next call needs no retry
        (await browser.GetApiAsync($"{path}?{EchoApi.RequireNonceParameter}={nonce}")).StatusCode.ShouldBe(HttpStatusCode.OK);
        ApiRequests(path).Count.ShouldBe(3);
    }

    [Test]
    public async Task the_bff_retries_one_time_only()
    {
        using var browser = _bff.CreateBrowser();
        await BffApp.SignInAsync(browser, providerQuery: $"test_sub={NewSubject()}");
        var path = $"/api/v1/{PathName()}";

        var response = await browser.GetApiAsync($"{path}?{EchoApi.RequireNonceParameter}=n1&{EchoApi.RejectNonceParameter}=1");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        ApiRequests(path).Count.ShouldBe(2);
    }

    [Test]
    public async Task a_post_body_is_sent_again_with_the_retry()
    {
        using var browser = _bff.CreateBrowser();
        await BffApp.SignInAsync(browser, providerQuery: $"test_sub={NewSubject()}");
        var path = $"/api/v1/{PathName()}";
        var request = new HttpRequestMessage(HttpMethod.Post, $"{path}?{EchoApi.RequireNonceParameter}=n2")
        {
            Content = new StringContent("""{"a":1}""", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-CSRF", "1");

        var response = await browser.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var requests = ApiRequests(path);
        requests.Count.ShouldBe(2);
        requests[1].Headers["Content-Type"].ShouldStartWith("application/json");
        requests[1].Headers["Content-Length"].ShouldBe("7");
    }

    [Test]
    public async Task a_body_over_the_retry_limit_gets_no_retry_but_the_next_request_has_the_nonce()
    {
        var body = new ByteArrayContent(new byte[DPoPProxy.MaxRetryBodyBytes + 1]);
        body.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        await TheNonceIsStoredWithoutARetryAsync(body);
    }

    [Test]
    public async Task a_body_without_a_content_length_gets_no_retry_but_the_next_request_has_the_nonce()
    {
        var body = new UnknownLengthContent("""{"a":1}"""u8.ToArray());
        body.Headers.ContentLength.ShouldBeNull();

        await TheNonceIsStoredWithoutARetryAsync(body);
    }

    // The BFF cannot hold the body to send it again, so the 401 goes to the browser. The BFF still stores the
    // nonce, so the next request to the URL has it in its first proof.
    private static async Task TheNonceIsStoredWithoutARetryAsync(HttpContent body)
    {
        using var browser = _bff.CreateBrowser();
        await BffApp.SignInAsync(browser, providerQuery: $"test_sub={NewSubject()}");
        var path = $"/api/v1/{PathName()}";
        var nonce = $"nonce-{Guid.NewGuid():N}";
        var url = $"{path}?{EchoApi.RequireNonceParameter}={nonce}";
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = body };
        request.Headers.Add("X-CSRF", "1");

        var response = await browser.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        ApiRequests(path).Count.ShouldBe(1);

        (await browser.SendApiAsync(HttpMethod.Post, url)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var requests = ApiRequests(path);
        requests.Count.ShouldBe(2);
        DPoPProofVerifier.Verify(requests[1].Headers["DPoP"], "POST", $"{BffApp.Instance.Api.Url}{path}").Nonce.ShouldBe(nonce);
    }

    /// <summary>
    /// A body that the client sends chunked, with no <c>Content-Length</c>.
    /// </summary>
    private sealed class UnknownLengthContent(byte[] bytes) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(bytes).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    [Test]
    public async Task auto_with_a_provider_that_lacks_dpop_uses_bearer_tokens()
    {
        await using var app = StartBff(FeatureMode.Auto, _plainProvider);
        using var browser = app.CreateBrowser();
        var subject = NewSubject();

        var login = await browser.GetAsync("/bff/login");
        BffApp.Query(login.Headers.Location!)["dpop_jkt"].ShouldBeNull();
        var callback = await browser.GetAsync((await BffApp.AuthorizeAsync(login.Headers.Location!, $"test_sub={subject}")).PathAndQuery);
        callback.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var echo = await (await browser.GetApiAsync($"/api/v1/{PathName()}")).ReadEchoAsync();

        app.Services.GetRequiredService<ProviderFeatures>().Active.DPoP.ShouldBeFalse();
        echo.Headers["Authorization"].ShouldStartWith("Bearer at-");
        echo.Headers.ShouldNotContainKey("DPoP");
        _plainProvider.TokenRequests.Single(t => t.Subject == subject).DPoP.ShouldBeNull();
        _plainProvider.UserInfoRequests.Single(r => r.Subject == subject).Scheme.ShouldBe("Bearer");
    }

    [Test]
    public async Task auto_with_a_provider_that_lists_dpop_binds_the_tokens()
    {
        await using var app = StartBff(FeatureMode.Auto, _provider);

        app.Services.GetRequiredService<ProviderFeatures>().Active.DPoP.ShouldBeTrue();
        using var browser = app.CreateBrowser();
        var login = await browser.GetAsync("/bff/login");
        BffApp.Query(login.Headers.Location!)["dpop_jkt"].ShouldBe(_key.Jkt);
    }

    [Test]
    public async Task disable_ignores_the_key_and_uses_bearer_tokens()
    {
        await using var app = StartBff(FeatureMode.Disable, _provider);
        using var browser = app.CreateBrowser();

        var login = await browser.GetAsync("/bff/login");

        BffApp.Query(login.Headers.Location!)["dpop_jkt"].ShouldBeNull();
        app.Services.GetRequiredService<ProviderFeatures>().Active.DPoP.ShouldBeFalse();
    }

    [Test]
    public void require_with_a_provider_that_lacks_dpop_stops_the_bff_at_startup()
    {
        var exception = Should.Throw<Exception>(() => StartBff(FeatureMode.Require, _plainProvider));

        exception.ShouldBeOfType<ProviderFeatureException>();
        exception.Message.ShouldContain("dpop_signing_alg_values_supported");
    }

    [Test]
    public void require_without_a_key_stops_the_bff_at_startup()
    {
        var exception = Should.Throw<Exception>(() => BffApp.StartAnotherInstance(new Dictionary<string, string?>
        {
            ["Auth:Features:DPoP"] = nameof(FeatureMode.Require),
        }, identityProvider: _provider));

        exception.ShouldBeOfType<ProviderFeatureException>();
    }

    [Test]
    public void a_key_without_a_private_part_stops_the_bff_at_startup()
    {
        var publicKey = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["kty"] = "EC",
            ["crv"] = "P-256",
            ["alg"] = "ES256",
            ["x"] = "f83OJ3D2xF1Bg8vub9tLe1gHMzV76e8Tus9uPHvRVEU",
            ["y"] = "x_FEzRu9m36HLN_tue659LNpXW6pCyStikYjKIWI5a0",
        });

        var exception = Should.Throw<Exception>(() => BffApp.StartAnotherInstance(new Dictionary<string, string?>
        {
            ["Auth:Features:DPoP"] = nameof(FeatureMode.Require),
            ["Auth:DPoPJsonWebKey"] = publicKey,
        }, identityProvider: _provider));

        exception.Message.ShouldContain("must be a private key");
        exception.Message.ShouldNotContain("f83OJ3D2");
    }

    private static BffApp StartBff(FeatureMode mode, FakeIdentityProvider provider) =>
        BffApp.StartAnotherInstance(new Dictionary<string, string?>
        {
            ["Auth:Features:DPoP"] = mode.ToString(),
            ["Auth:DPoPJsonWebKey"] = _key.Jwk,
        }, identityProvider: provider);

    // A path of its own, so that parallel tests do not see each other's requests at the API
    private static string PathName() => $"dpop-{Guid.NewGuid():N}";

    private static List<EchoedRequest> ApiRequests(string path) =>
        BffApp.Instance.Api.Requests.Where(r => r.Path == path).ToList();

    private static string NewSubject() => $"user-{Guid.NewGuid():N}";
}
