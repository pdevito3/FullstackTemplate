namespace FullstackTemplate.Bff.Tests.Unit;

using System.Security.Claims;
using FullstackTemplate.Bff.ApiEndpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Shouldly;

public class BffApiAuthenticationServiceTests
{
    [Test]
    public async Task a_challenge_redirect_on_a_bff_api_endpoint_becomes_401_with_no_location_and_no_cookies()
    {
        var context = ContextFor(new EndpointMetadataCollection(BffApiEndpointMetadata.Instance));
        var service = new BffApiAuthenticationService(new RedirectingAuthenticationService());

        await service.ChallengeAsync(context, null, null);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
        context.Response.Headers.Location.ShouldBeEmpty();
        context.Response.Headers.SetCookie.ShouldBeEmpty();
    }

    [Test]
    public async Task a_forbid_redirect_on_a_bff_api_endpoint_becomes_403_with_no_location_and_no_cookies()
    {
        var context = ContextFor(new EndpointMetadataCollection(BffApiEndpointMetadata.Instance));
        var service = new BffApiAuthenticationService(new RedirectingAuthenticationService());

        await service.ForbidAsync(context, null, null);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        context.Response.Headers.Location.ShouldBeEmpty();
        context.Response.Headers.SetCookie.ShouldBeEmpty();
    }

    [Test]
    public async Task a_challenge_redirect_on_another_endpoint_stays_a_redirect()
    {
        var context = ContextFor(EndpointMetadataCollection.Empty);
        var service = new BffApiAuthenticationService(new RedirectingAuthenticationService());

        await service.ChallengeAsync(context, null, null);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status302Found);
        context.Response.Headers.Location.ToString().ShouldBe(RedirectingAuthenticationService.Location);
        context.Response.Headers.SetCookie.Count.ShouldBe(2);
    }

    [Test]
    public async Task a_challenge_with_no_endpoint_stays_a_redirect()
    {
        var context = new DefaultHttpContext();
        var service = new BffApiAuthenticationService(new RedirectingAuthenticationService());

        await service.ForbidAsync(context, null, null);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status302Found);
    }

    private static DefaultHttpContext ContextFor(EndpointMetadataCollection metadata)
    {
        var context = new DefaultHttpContext();
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, metadata, "test"));
        return context;
    }

    /// <summary>
    /// A challenge and a forbid as the cookie and OIDC handlers do them: a 302 with a Location and cookies.
    /// </summary>
    private sealed class RedirectingAuthenticationService : IAuthenticationService
    {
        public const string Location = "https://idp.example/authorize";

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Redirect(context);

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Redirect(context);

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(AuthenticateResult.NoResult());

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;

        private static Task Redirect(HttpContext context)
        {
            context.Response.StatusCode = StatusCodes.Status302Found;
            context.Response.Headers.Location = Location;
            context.Response.Headers.Append("Set-Cookie", ".AspNetCore.Correlation.x=1; path=/signin-oidc");
            context.Response.Headers.Append("Set-Cookie", ".AspNetCore.OpenIdConnect.Nonce.x=1; path=/signin-oidc");
            return Task.CompletedTask;
        }
    }
}
