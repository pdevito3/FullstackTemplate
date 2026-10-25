namespace FullstackTemplate.Bff.Tests.Integration;

using System.Net;
using Shouldly;

public class LoginEndpointTests
{
    [Test]
    public async Task login_redirects_to_the_authorize_endpoint_with_code_and_pkce()
    {
        using var browser = BffApp.Instance.CreateBrowser();

        var response = await browser.GetAsync("/bff/login");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = response.Headers.Location!;
        location.GetLeftPart(UriPartial.Path).ShouldBe($"{BffApp.Instance.IdentityProvider.Issuer}/authorize");
        var query = BffApp.Query(location);
        query["response_type"].ShouldBe("code");
        // The handler omits response_mode=query, the default for the code flow
        query["response_mode"].ShouldBeNull();
        query["code_challenge_method"].ShouldBe("S256");
        query["redirect_uri"].ShouldBe("https://localhost/signin-oidc");
        query["scope"].ShouldBe("openid profile email offline_access test-api");
        query["prompt"].ShouldBeNull();
    }

    [Test]
    [Arguments(".AspNetCore.Correlation.")]
    [Arguments(".AspNetCore.OpenIdConnect.Nonce.")]
    public async Task login_sets_the_correlation_and_nonce_cookies_with_samesite_lax(string cookiePrefix)
    {
        using var browser = BffApp.Instance.CreateBrowser();

        var response = await browser.GetAsync("/bff/login");

        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(cookiePrefix, StringComparison.Ordinal));
        cookie.ShouldContain("samesite=lax", Case.Insensitive);
        cookie.ShouldContain("secure", Case.Insensitive);
        cookie.ShouldContain("httponly", Case.Insensitive);
    }

    [Test]
    public async Task a_supported_prompt_goes_to_the_authorize_request()
    {
        using var browser = BffApp.Instance.CreateBrowser();

        var response = await browser.GetAsync("/bff/login?prompt=login");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        BffApp.Query(response.Headers.Location!)["prompt"].ShouldBe("login");
    }

    [Test]
    [Arguments("select_account")]
    [Arguments("")]
    public async Task a_prompt_that_discovery_does_not_list_gets_400_problem(string prompt)
    {
        using var browser = BffApp.Instance.CreateBrowser();

        var response = await browser.GetAsync($"/bff/login?prompt={prompt}");

        await ShouldBeProblemAsync(response, "Invalid prompt value", "prompt");
    }

    [Test]
    [Arguments("https%3A%2F%2Fevil.example")]
    [Arguments("%2F%2Fevil.example")]
    [Arguments("%2F%5Cevil.example")]
    [Arguments("evil")]
    [Arguments("%40evil.example")]
    public async Task a_return_url_that_is_not_local_gets_400_problem(string returnUrl)
    {
        using var browser = BffApp.Instance.CreateBrowser();

        var response = await browser.GetAsync($"/bff/login?returnUrl={returnUrl}");

        await ShouldBeProblemAsync(response, "Invalid return url", "returnUrl");
    }

    [Test]
    [Arguments("?returnUrl=%2Fweather%3Fcity%3Dx", "/weather?city=x")]
    [Arguments("?returnUrl=%2Fassets%2F42", "/assets/42")]
    [Arguments("", "/")]
    public async Task the_callback_redirects_to_the_return_url_on_the_spa_dev_server(string loginQuery, string expected)
    {
        using var browser = BffApp.Instance.CreateBrowser();

        var callback = await BffApp.SignInAsync(browser, loginQuery, "test_sub=user-return");

        callback.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        callback.Headers.Location!.OriginalString.ShouldBe(BffApp.SpaDevServer + expected);
    }

    [Test]
    [Arguments("%2F%40evil.example", "/@evil.example")]
    [Arguments("%2F%252F%252Fevil.example", "/%2F%2Fevil.example")]
    [Arguments("%2Fweather%3Fnext%3D%2F%2Fevil.example", "/weather?next=//evil.example")]
    public async Task a_local_return_url_that_names_another_host_stays_on_the_spa_dev_server(string returnUrl, string expected)
    {
        using var browser = BffApp.Instance.CreateBrowser();

        var callback = await BffApp.SignInAsync(browser, $"?returnUrl={returnUrl}", "test_sub=user-return-host");

        callback.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        callback.Headers.Location!.OriginalString.ShouldBe(BffApp.SpaDevServer + expected);
        callback.Headers.Location!.Authority.ShouldBe(new Uri(BffApp.SpaDevServer).Authority);
    }

    [Test]
    public async Task without_a_spa_dev_server_the_callback_redirects_to_the_local_return_url()
    {
        await using var app = BffApp.StartAnotherInstance(new Dictionary<string, string?>
        {
            ["services:webfrontend:http:0"] = "",
        });
        using var browser = app.CreateBrowser();

        var callback = await BffApp.SignInAsync(browser, "?returnUrl=%2Fassets%2F42%3Ftab%3Dx", "test_sub=user-return-published");

        callback.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        callback.Headers.Location!.OriginalString.ShouldBe("/assets/42?tab=x");
    }

    [Test]
    public async Task an_error_for_a_prompt_login_redirects_to_the_return_url_with_no_session()
    {
        using var browser = BffApp.Instance.CreateBrowser();

        var callback = await BffApp.SignInAsync(browser, "?prompt=none&returnUrl=%2Fback", "test_error=login_required");

        callback.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        callback.Headers.Location!.OriginalString.ShouldBe($"{BffApp.SpaDevServer}/back");
        (await browser.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    public static async Task ShouldBeProblemAsync(HttpResponseMessage response, string title, string field)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var problem = await response.ReadJsonAsync();
        problem.GetProperty("title").GetString().ShouldBe(title);
        problem.GetProperty("status").GetInt32().ShouldBe(400);
        problem.GetProperty("errors").GetProperty(field).GetArrayLength().ShouldBe(1);
    }
}
