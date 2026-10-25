using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using FullstackTemplate.Bff.Logout;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace FullstackTemplate.Bff.User;

/// <summary>
/// <c>/bff/user</c> returns the claims of the BFF session to the SPA. It is a BFF API endpoint, so it needs
/// <c>X-CSRF: 1</c>. An anonymous request gets a 204 with an empty body. A signed-out visitor is a normal state, and a
/// browser logs each 4xx response as an error in the console.
/// </summary>
public static class UserEndpoint
{
    public const string Path = "/bff/user";

    public const string LogoutUrlClaim = "bff:logout_url";
    public const string SessionExpiresInClaim = "bff:session_expires_in";
    public const string SessionStateClaim = "bff:session_state";

    // The OIDC handler stores the session_state of the authorize response under this key.
    private const string SessionStateItem = ".sessionState";

    public static async Task HandleAsync(HttpContext context, TimeProvider timeProvider)
    {
        var result = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!result.Succeeded)
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return;
        }

        var claims = new List<ClaimRecord>();
        claims.AddRange(result.Principal.Claims.Select(c => new ClaimRecord(c.Type, c.Value)));

        if (!result.Principal.HasClaim(c => c.Type == LogoutUrlClaim))
            claims.Add(new ClaimRecord(LogoutUrlClaim, LogoutUrl(context.Request.PathBase, result.Principal.FindFirst("sid")?.Value)));

        if (result.Properties.ExpiresUtc is { } expiresUtc)
            claims.Add(new ClaimRecord(SessionExpiresInClaim, Math.Round((expiresUtc - timeProvider.GetUtcNow()).TotalSeconds)));

        if (result.Properties.Items.TryGetValue(SessionStateItem, out var sessionState) && sessionState is not null)
            claims.Add(new ClaimRecord(SessionStateClaim, sessionState));

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(claims), context.RequestAborted);
    }

    /// <summary>
    /// <c>PathBase + /bff/logout</c>, with <c>?sid=</c> when the BFF session has a <c>sid</c> claim.
    /// </summary>
    public static string LogoutUrl(PathString pathBase, string? sessionId)
    {
        var url = pathBase + LogoutEndpoint.Path;
        return sessionId is null ? url : $"{url}?sid={UrlEncoder.Default.Encode(sessionId)}";
    }

    /// <summary>
    /// The cookie <c>OnCheckSlidingExpiration</c> event. <c>/bff/user?slide=false</c> reads the BFF session
    /// without a renewal of its sliding expiry, so that a poll of the session state does not keep it alive.
    /// </summary>
    public static Task SuppressSlideWhenRequested(CookieSlidingExpirationContext context)
    {
        if (context.Request.Path == Path && context.Request.Query["slide"] == "false")
            context.ShouldRenew = false;

        return Task.CompletedTask;
    }

    /// <summary>
    /// One item of the <c>/bff/user</c> array. <c>valueType</c> is always null.
    /// </summary>
    public sealed record ClaimRecord(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("value")] object Value)
    {
        [JsonPropertyName("valueType")]
        public string? ValueType => null;
    }
}
