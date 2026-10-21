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
    public async Task a_return_url_that_is_not_local_gets_400_problem(string returnUrl)
    {
        using var browser = BffApp.Instance.CreateBrowser();

        var response = await browser.GetAsync($"/bff/login?returnUrl={returnUrl}");

        await ShouldBeProblemAsync(response, "Invalid return url", "returnUrl");
    }

    [Test]
    [Arguments("?returnUrl=%2Fweather%3Fcity%3Dx", "/weather?city=x")]
    [Arguments("", "/")]
    public async Task the_callback_redirects_to_the_return_url(string loginQuery, string expected)
    {
        using var browser = BffApp.Instance.CreateBrowser();

        var callback = await BffApp.SignInAsync(browser, loginQuery, "test_sub=user-return");

        callback.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        callback.Headers.Location!.OriginalString.ShouldBe(expected);
    }

    [Test]
    public async Task an_error_for_a_prompt_login_redirects_to_the_return_url_with_no_session()
    {
        using var browser = BffApp.Instance.CreateBrowser();

        var callback = await BffApp.SignInAsync(browser, "?prompt=none&returnUrl=%2Fback", "test_error=login_required");

        callback.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        callback.Headers.Location!.OriginalString.ShouldBe("/back");
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
