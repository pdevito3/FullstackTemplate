namespace FullstackTemplate.Bff.Tests.Fakes;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// A fallback authorization policy that only a test request turns on, so that a test can make an endpoint of the
/// real pipeline challenge or forbid. The <c>X-Test-Require</c> header selects the rule: <c>user</c> needs a
/// signed-in user, and <c>admin</c> needs a <c>role</c> claim <c>admin</c>. A request with no header passes.
/// </summary>
public static class TestAuthorization
{
    public const string HeaderName = "X-Test-Require";

    public static IServiceCollection AddTestAuthorization(this IServiceCollection services) =>
        services.Configure<AuthorizationOptions>(options =>
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAssertion(Allows).Build());

    private static bool Allows(AuthorizationHandlerContext context)
    {
        if (context.Resource is not HttpContext httpContext)
            return true;

        return httpContext.Request.Headers[HeaderName].ToString() switch
        {
            "user" => context.User.Identity?.IsAuthenticated == true,
            "admin" => context.User.HasClaim("role", "admin"),
            _ => true,
        };
    }
}
