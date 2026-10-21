using System.Security.Claims;
using System.Text.Json;
using FullstackTemplate.Bff.Management;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using ILogger = Serilog.ILogger;

namespace FullstackTemplate.Bff.BackChannel;

/// <summary>
/// <c>POST /bff/backchannel</c>, the receiver of OpenID Connect Back-Channel Logout. The identity provider
/// calls it with a <c>logout_token</c> form field.
/// <para>
/// The endpoint validates the logout token and answers 200, or 400 for a missing or invalid token. The BFF
/// session lives only in the cookie, so the endpoint cannot end a BFF session yet.
/// </para>
/// </summary>
public static class BackChannelLogoutEndpoint
{
    public const string Path = "/bff/backchannel";

    private const string BackChannelLogoutEvent = "http://schemas.openid.net/event/backchannel-logout";

    private static readonly ILogger Logger = Log.ForContext(typeof(BackChannelLogoutEndpoint));

    public static async Task HandleAsync(HttpContext context, IOptionsMonitor<OpenIdConnectOptions> oidcOptions)
    {
        context.Response.Headers.Append("Cache-Control", "no-cache, no-store");
        context.Response.Headers.Append("Pragma", "no-cache");

        var logoutToken = context.Request.HasFormContentType
            ? (await context.Request.ReadFormAsync(context.RequestAborted))["logout_token"].FirstOrDefault()
            : null;

        if (string.IsNullOrWhiteSpace(logoutToken))
        {
            Logger.Here().Information("Rejected a back-channel logout request with no logout_token");
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var identity = await ValidateAsync(logoutToken, oidcOptions, context.RequestAborted);
        if (identity is null)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        Logger.Here().Information(
            "Received a valid back-channel logout for sub {Subject} and sid {SessionId}. The BFF has no server-side sessions to end",
            identity.FindFirst("sub")?.Value, identity.FindFirst("sid")?.Value);
        context.Response.StatusCode = StatusCodes.Status200OK;
    }

    /// <summary>
    /// The checks of the logout token (Back-Channel Logout 1.0 section 2.6). Returns null when a check fails.
    /// </summary>
    public static async Task<ClaimsIdentity?> ValidateAsync(
        string logoutToken,
        IOptionsMonitor<OpenIdConnectOptions> oidcOptions,
        CancellationToken cancellationToken)
    {
        var options = oidcOptions.Get(OpenIdConnectDefaults.AuthenticationScheme);
        var configuration = await OidcDiscovery.GetConfigurationAsync(oidcOptions, cancellationToken)
            ?? throw new InvalidOperationException("The OIDC handler has no discovery document.");

        var parameters = new TokenValidationParameters
        {
            ValidIssuer = configuration.Issuer,
            ValidAudience = options.ClientId,
            IssuerSigningKeys = configuration.SigningKeys,
            RequireSignedTokens = true,
            NameClaimType = "name",
            RoleClaimType = "role",
        };

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(logoutToken, parameters);
        if (!result.IsValid || result.SecurityToken is not JsonWebToken token)
        {
            Logger.Here().Information(result.Exception, "Rejected a logout token that failed validation");
            return null;
        }

        var failure = CheckClaims(token, result.ClaimsIdentity);
        if (failure is not null)
        {
            Logger.Here().Information("Rejected a logout token: {Reason}", failure);
            return null;
        }

        return result.ClaimsIdentity;
    }

    private static string? CheckClaims(JsonWebToken token, ClaimsIdentity identity)
    {
        // A typ is optional, but when present it must name a logout token, so that another JWT of the same
        // issuer, such as an ID token, cannot pass as one. Media type names compare case-insensitive.
        if (!string.IsNullOrEmpty(token.Typ)
            && !string.Equals(token.Typ, "logout+jwt", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(token.Typ, "application/logout+jwt", StringComparison.OrdinalIgnoreCase))
            return $"the typ header is {token.Typ}, not logout+jwt";

        if (identity.FindFirst("iat") is null)
            return "the iat claim is missing";

        if (identity.FindFirst("sub") is null && identity.FindFirst("sid") is null)
            return "the sub and sid claims are both missing";

        if (!string.IsNullOrWhiteSpace(identity.FindFirst("nonce")?.Value))
            return "the token has a nonce claim";

        var events = identity.FindFirst("events")?.Value;
        if (string.IsNullOrWhiteSpace(events))
            return "the events claim is missing";

        try
        {
            using var document = JsonDocument.Parse(events);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty(BackChannelLogoutEvent, out _))
                return "the events claim has no back-channel logout member";
        }
        catch (JsonException)
        {
            return "the events claim is not a JSON object";
        }

        return null;
    }
}
