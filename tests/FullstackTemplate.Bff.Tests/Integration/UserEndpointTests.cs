namespace FullstackTemplate.Bff.Tests.Integration;

using System.Net;
using System.Text.Json;
using Shouldly;

public class UserEndpointTests
{
    [Test]
    public async Task an_anonymous_request_gets_204_with_an_empty_body()
    {
        using var browser = BffApp.Instance.CreateBrowser();

        var response = await browser.GetApiAsync("/bff/user");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).ShouldBeEmpty();
    }

    [Test]
    public async Task a_signed_in_request_gets_the_session_claims_and_then_the_management_claims()
    {
        using var browser = BffApp.Instance.CreateBrowser();
        await BffApp.SignInAsync(browser, providerQuery: "test_sub=user-claims&test_sid=sid%2B1");

        var response = await browser.GetApiAsync("/bff/user");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        var claims = (await response.ReadJsonAsync()).EnumerateArray().ToList();

        foreach (var claim in claims)
        {
            claim.EnumerateObject().Select(p => p.Name).ShouldBe(["type", "value", "valueType"]);
            claim.GetProperty("valueType").ValueKind.ShouldBe(JsonValueKind.Null);
        }

        var types = claims.Select(c => c.GetProperty("type").GetString()).ToList();
        types.ShouldContain("sub");
        types.ShouldContain("sid");
        types.ShouldContain("email");
        types[^2..].ShouldBe(["bff:logout_url", "bff:session_expires_in"]);

        Value(claims, "sub").GetString().ShouldBe("user-claims");
        Value(claims, "bff:logout_url").GetString().ShouldBe("/bff/logout?sid=sid%2B1");

        var expiresIn = Value(claims, "bff:session_expires_in");
        expiresIn.ValueKind.ShouldBe(JsonValueKind.Number);
        expiresIn.GetDouble().ShouldBeInRange(TimeSpan.FromDays(14).TotalSeconds - 60, TimeSpan.FromDays(14).TotalSeconds);
    }

    [Test]
    public async Task the_logout_url_has_no_sid_when_the_session_has_none()
    {
        using var browser = BffApp.Instance.CreateBrowser();
        await BffApp.SignInAsync(browser, providerQuery: "test_sub=user-no-sid");

        var claims = (await (await browser.GetApiAsync("/bff/user")).ReadJsonAsync()).EnumerateArray().ToList();

        Value(claims, "bff:logout_url").GetString().ShouldBe("/bff/logout");
    }

    [Test]
    public async Task the_session_state_of_the_authorize_response_comes_last()
    {
        using var browser = BffApp.Instance.CreateBrowser();
        await BffApp.SignInAsync(browser, providerQuery: "test_sub=user-state&test_session_state=state-1");

        var claims = (await (await browser.GetApiAsync("/bff/user")).ReadJsonAsync()).EnumerateArray().ToList();

        claims[^1].GetProperty("type").GetString().ShouldBe("bff:session_state");
        claims[^1].GetProperty("value").GetString().ShouldBe("state-1");
    }

    [Test]
    public async Task the_callback_sets_the_session_cookie_with_the_current_attributes()
    {
        using var browser = BffApp.Instance.CreateBrowser();

        var callback = await BffApp.SignInAsync(browser, providerQuery: "test_sub=user-cookie");

        var cookie = callback.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("__Host-bff=", StringComparison.Ordinal));
        cookie.ShouldContain("path=/", Case.Insensitive);
        cookie.ShouldContain("secure", Case.Insensitive);
        cookie.ShouldContain("samesite=strict", Case.Insensitive);
        cookie.ShouldContain("httponly", Case.Insensitive);
        cookie.ShouldNotContain("domain=", Case.Insensitive);
    }

    private static JsonElement Value(List<JsonElement> claims, string type) =>
        claims.First(c => c.GetProperty("type").GetString() == type).GetProperty("value");
}
