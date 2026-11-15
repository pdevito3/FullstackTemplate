namespace FullstackTemplate.Bff.Tests.Integration;

using System.Net;
using FullstackTemplate.Bff.Login;
using FullstackTemplate.Bff.Management;
using FullstackTemplate.Bff.Sessions;
using FullstackTemplate.Bff.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Events;
using Shouldly;

public class AuthFailurePageTests
{
    private const string SignInFailed = $"{BffApp.SpaDevServer}/sign-in-failed";
    private const string SignOutFailed = $"{BffApp.SpaDevServer}/sign-out-failed";

    [Test]
    public async Task a_callback_with_an_invalid_state_sends_the_navigation_to_the_sign_in_failed_page()
    {
        // Arrange
        using var browser = Navigating(BffApp.Instance.CreateBrowser());
        var issuer = Uri.EscapeDataString(BffApp.Instance.IdentityProvider.Issuer);

        // Act
        var callback = await browser.GetAsync($"/signin-oidc?code=x&state=not-a-state&iss={issuer}");

        // Assert
        ShouldGoTo(callback, SignInFailed);
        BffApp.SessionCookieValue(callback).ShouldBeNull();
    }

    [Test]
    [Arguments("test_error=access_denied")]
    [Arguments("test_iss=https%3A%2F%2Fevil.example")]
    [Arguments("test_aud=other-api")]
    [Arguments("test_nonce=wrong")]
    public async Task a_refused_callback_sends_the_navigation_to_the_sign_in_failed_page(string providerQuery)
    {
        // Arrange
        using var browser = Navigating(BffApp.Instance.CreateBrowser());

        // Act
        var callback = await BffApp.SignInAsync(browser, providerQuery: $"test_sub={NewSubject()}&{providerQuery}");

        // Assert
        ShouldGoTo(callback, SignInFailed);
        BffApp.SessionCookieValue(callback).ShouldBeNull();
        (await browser.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task a_refused_callback_keeps_the_problem_for_a_fetch()
    {
        // Arrange
        using var browser = BffApp.Instance.CreateBrowser();
        browser.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "cors");

        // Act
        var callback = await BffApp.SignInAsync(browser, providerQuery: "test_error=access_denied");

        // Assert
        await LoginEndpointTests.ShouldBeProblemAsync(callback, CallbackRejectedException.Title, CallbackRejection.CallbackField);
    }

    [Test]
    public async Task a_callback_that_is_not_a_get_sends_the_navigation_to_the_sign_in_failed_page()
    {
        // Arrange
        var subject = NewSubject();
        using var browser = Navigating(BffApp.Instance.CreateBrowser());
        var login = await browser.GetAsync("/bff/login");
        var callback = await BffApp.AuthorizeAsync(login.Headers.Location!, $"test_sub={subject}");

        // Act
        var response = await browser.PostAsync("/signin-oidc" + callback.Query, new FormUrlEncodedContent([]));

        // Assert
        ShouldGoTo(response, SignInFailed);
        BffApp.Instance.IdentityProvider.TokenRequests.ShouldNotContain(r => r.Subject == subject);
    }

    [Test]
    [Arguments("?returnUrl=https%3A%2F%2Fevil.example")]
    [Arguments("?prompt=not-a-prompt")]
    public async Task a_refused_login_sends_the_navigation_to_the_sign_in_failed_page(string query)
    {
        // Arrange
        using var browser = Navigating(BffApp.Instance.CreateBrowser());

        // Act
        var response = await browser.GetAsync($"/bff/login{query}");

        // Assert
        ShouldGoTo(response, SignInFailed);
    }

    [Test]
    public async Task a_refused_login_navigation_logs_the_refusal()
    {
        // Arrange
        using var browser = Navigating(BffApp.Instance.CreateBrowser());
        var prompt = $"not-a-prompt-{Guid.NewGuid():N}";

        // Act
        var response = await browser.GetAsync($"/bff/login?prompt={prompt}");

        // Assert
        ShouldGoTo(response, SignInFailed);
        BffApp.Logs.WithPropertyContaining(prompt).ShouldHaveSingleItem().Level.ShouldBe(LogEventLevel.Warning);
    }

    [Test]
    [Arguments("?sid=other")]
    [Arguments("?sid=sid-refused&returnUrl=https%3A%2F%2Fevil.example")]
    public async Task a_refused_logout_sends_the_navigation_to_the_sign_out_failed_page_and_keeps_the_session(string query)
    {
        // Arrange
        using var browser = Navigating(BffApp.Instance.CreateBrowser());
        await BffApp.SignInAsync(browser, providerQuery: $"test_sub={NewSubject()}&test_sid=sid-refused");

        // Act
        var response = await browser.GetAsync($"/bff/logout{query}");

        // Assert
        ShouldGoTo(response, SignOutFailed);
        (await browser.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task a_callback_that_cannot_end_the_old_session_sends_the_navigation_to_the_sign_in_failed_page()
    {
        // Arrange
        await using var app = BffApp.StartAnotherInstance(services: services => services.ConfigureDbContext<BffDbContext>(
            options => options.AddInterceptors(new FailingCommandInterceptor("bff_sessions.delete"))));
        using var browser = Navigating(app.CreateBrowser());
        await BffApp.SignInAsync(browser, providerQuery: $"test_sub={NewSubject()}");

        // Act
        var callback = await BffApp.SignInAsync(browser, providerQuery: $"test_sub={NewSubject()}");

        // Assert
        ShouldGoTo(callback, SignInFailed);
    }

    [Test]
    public async Task a_logout_that_cannot_end_the_session_sends_the_navigation_to_the_sign_out_failed_page()
    {
        // Arrange
        await using var app = BffApp.StartAnotherInstance(services: services => services.ConfigureDbContext<BffDbContext>(
            options => options.AddInterceptors(new FailingCommandInterceptor("bff_sessions.delete"))));
        using var browser = Navigating(app.CreateBrowser());
        await BffApp.SignInAsync(browser, providerQuery: $"test_sub={NewSubject()}&test_sid=sid-kept");

        // Act
        var response = await browser.GetAsync("/bff/logout?sid=sid-kept");

        // Assert
        ShouldGoTo(response, SignOutFailed);
        (await browser.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task a_logout_that_cannot_end_the_session_keeps_the_problem_for_a_fetch()
    {
        // Arrange
        await using var app = BffApp.StartAnotherInstance(services: services => services.ConfigureDbContext<BffDbContext>(
            options => options.AddInterceptors(new FailingCommandInterceptor("bff_sessions.delete"))));
        using var browser = app.CreateBrowser();
        await BffApp.SignInAsync(browser, providerQuery: $"test_sub={NewSubject()}&test_sid=sid-fetch");
        browser.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "cors");

        // Act
        var response = await browser.GetAsync("/bff/logout?sid=sid-fetch");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(ManagementProblem.ContentType);
    }

    [Test]
    [Arguments("")]
    [Arguments("?state=not-a-state")]
    public async Task a_return_from_the_provider_logout_with_no_state_that_the_bff_can_read_goes_to_the_home_page(string query)
    {
        // Arrange
        using var browser = Navigating(BffApp.Instance.CreateBrowser());

        // Act
        var response = await browser.GetAsync($"/signout-callback-oidc{query}");

        // Assert
        ShouldGoTo(response, "/");
    }

    private static HttpClient Navigating(HttpClient browser)
    {
        browser.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
        return browser;
    }

    private static void ShouldGoTo(HttpResponseMessage response, string location)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.ShouldBe(location);
    }

    private static string NewSubject() => $"user-{Guid.NewGuid():N}";
}
