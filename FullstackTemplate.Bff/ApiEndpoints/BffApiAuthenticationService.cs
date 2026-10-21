using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Net.Http.Headers;

namespace FullstackTemplate.Bff.ApiEndpoints;

/// <summary>
/// Decorates <see cref="IAuthenticationService"/>. On a BFF API endpoint, a challenge or a forbid that gives a
/// 302 becomes a 401 or a 403, because a <c>fetch</c> call cannot follow a redirect to the identity provider.
/// The response then has no <c>Location</c> and no <c>Set-Cookie</c> header, so the OIDC correlation and nonce
/// cookies of the challenge do not reach the browser. Other endpoints, such as login and logout, keep the 302.
/// </summary>
public sealed class BffApiAuthenticationService(IAuthenticationService inner) : IAuthenticationService
{
    public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
        inner.AuthenticateAsync(context, scheme);

    public async Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
    {
        await inner.ChallengeAsync(context, scheme, properties);
        RewriteRedirect(context, StatusCodes.Status401Unauthorized);
    }

    public async Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
    {
        await inner.ForbidAsync(context, scheme, properties);
        RewriteRedirect(context, StatusCodes.Status403Forbidden);
    }

    public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) =>
        inner.SignInAsync(context, scheme, principal, properties);

    public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
        inner.SignOutAsync(context, scheme, properties);

    private static void RewriteRedirect(HttpContext context, int statusCode)
    {
        if (context.Response.StatusCode != StatusCodes.Status302Found || !context.IsBffApiEndpoint())
            return;

        context.Response.StatusCode = statusCode;
        context.Response.Headers.Remove(HeaderNames.Location);
        context.Response.Headers.Remove(HeaderNames.SetCookie);
    }
}

public static class BffApiAuthenticationServiceExtensions
{
    /// <summary>
    /// Wraps the registered <see cref="IAuthenticationService"/> in <see cref="BffApiAuthenticationService"/>.
    /// Call it after <c>AddAuthentication</c>. The inner service keeps its own registration, so the framework
    /// authentication metrics still apply.
    /// </summary>
    public static IServiceCollection AddBffApiStatusCodes(this IServiceCollection services)
    {
        var inner = services.LastOrDefault(d => d.ServiceType == typeof(IAuthenticationService))
            ?? throw new InvalidOperationException("Call AddAuthentication before AddBffApiStatusCodes.");

        services.Remove(inner);
        services.Add(ServiceDescriptor.Describe(
            typeof(IAuthenticationService),
            sp => new BffApiAuthenticationService(CreateInner(sp, inner)),
            inner.Lifetime));

        return services;
    }

    private static IAuthenticationService CreateInner(IServiceProvider services, ServiceDescriptor descriptor) =>
        (IAuthenticationService)(descriptor switch
        {
            { ImplementationInstance: { } instance } => instance,
            { ImplementationFactory: { } factory } => factory(services),
            { ImplementationType: { } type } => ActivatorUtilities.CreateInstance(services, type),
            _ => throw new InvalidOperationException("The IAuthenticationService registration has no implementation."),
        });
}
