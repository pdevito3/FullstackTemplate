namespace FullstackTemplate.Bff.Tests.Unit;

using System.Security.Claims;
using System.Text;
using FullstackTemplate.Bff.BackChannel;
using FullstackTemplate.Bff.Login;
using FullstackTemplate.Bff.User;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
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

    [Test]
    public async Task backchannel_returns_400_when_the_handler_has_no_discovery_document()
    {
        var context = BackChannelRequest();
        var options = new OpenIdConnectOptions { Configuration = null, ConfigurationManager = null };

        await BackChannelLogoutEndpoint.HandleAsync(context, new FixedOptionsMonitor<OpenIdConnectOptions>(options));

        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        context.Response.Headers.CacheControl.ToString().ShouldBe("no-cache, no-store");
    }

    [Test]
    public async Task backchannel_returns_400_when_the_discovery_document_cannot_be_read()
    {
        var context = BackChannelRequest();
        var options = new OpenIdConnectOptions { Configuration = null, ConfigurationManager = new FailingConfigurationManager() };

        await BackChannelLogoutEndpoint.HandleAsync(context, new FixedOptionsMonitor<OpenIdConnectOptions>(options));

        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        context.Response.Headers.CacheControl.ToString().ShouldBe("no-cache, no-store");
    }

    private static DefaultHttpContext BackChannelRequest()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("logout_token=a.b.c"));
        return context;
    }

    private sealed class FailingConfigurationManager : IConfigurationManager<OpenIdConnectConfiguration>
    {
        public Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel) =>
            throw new InvalidOperationException("IDX20803: Unable to obtain configuration.");

        public void RequestRefresh()
        {
        }
    }

    private sealed class FixedOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
