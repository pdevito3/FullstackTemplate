namespace FullstackTemplate.Bff.Tests.Unit;

using System.Security.Claims;
using FullstackTemplate.Bff.Login;
using FullstackTemplate.Bff.User;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Shouldly;

public class ManagementEndpointUnitTests
{
    [Test]
    public async Task login_returns_404_when_the_handler_has_no_discovery_document()
    {
        var context = new DefaultHttpContext();
        var options = new OpenIdConnectOptions { Configuration = null, ConfigurationManager = null };

        await LoginEndpoint.HandleAsync(context, new FixedOptionsMonitor<OpenIdConnectOptions>(options));

        context.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Test]
    [Arguments("", null, "/bff/logout")]
    [Arguments("", "abc", "/bff/logout?sid=abc")]
    [Arguments("/base", "a b&c", "/base/bff/logout?sid=a%20b%26c")]
    public void the_logout_url_has_the_path_base_and_the_encoded_sid(string pathBase, string? sessionId, string expected)
    {
        UserEndpoint.LogoutUrl(new PathString(pathBase), sessionId).ShouldBe(expected);
    }

    [Test]
    [Arguments("/bff/user", "?slide=false", false)]
    [Arguments("/bff/user", "", true)]
    [Arguments("/bff/user", "?slide=true", true)]
    [Arguments("/api/v1/x", "?slide=false", true)]
    public async Task only_bff_user_with_slide_false_skips_the_sliding_renewal(string path, string query, bool shouldRenew)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = path;
        httpContext.Request.QueryString = new QueryString(query);
        var scheme = new AuthenticationScheme(CookieAuthenticationDefaults.AuthenticationScheme, null, typeof(CookieAuthenticationHandler));
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(), CookieAuthenticationDefaults.AuthenticationScheme);
        var context = new CookieSlidingExpirationContext(httpContext, scheme, new CookieAuthenticationOptions(), ticket,
            TimeSpan.FromDays(8), TimeSpan.FromDays(6))
        {
            ShouldRenew = true,
        };

        await UserEndpoint.SuppressSlideWhenRequested(context);

        context.ShouldRenew.ShouldBe(shouldRenew);
    }

    private sealed class FixedOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
