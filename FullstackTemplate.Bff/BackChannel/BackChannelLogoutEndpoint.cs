using System.Security.Claims;
using System.Text.Json;
using FullstackTemplate.Bff.Authentication;
using FullstackTemplate.Bff.Management;
using FullstackTemplate.Bff.Sessions;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using ILogger = Serilog.ILogger;

namespace FullstackTemplate.Bff.BackChannel;

/// <summary>
/// <c>POST /bff/backchannel</c>, the receiver of OpenID Connect Back-Channel Logout. The identity provider
/// calls it with a <c>logout_token</c> form field.
/// <para>
/// The endpoint validates the logout token, rejects a replay of its <c>jti</c>, and ends the BFF sessions of the
/// IdP session: the rows with the <c>sid</c> of the token, or all the rows of its <c>sub</c> when it has no
/// <c>sid</c>. It answers 200, or 400 for a missing, invalid, or replayed token.
/// </para>
/// </summary>
public static class BackChannelLogoutEndpoint
{
    public const string Path = "/bff/backchannel";

    private const string BackChannelLogoutEvent = "http://schemas.openid.net/event/backchannel-logout";

    private static readonly ILogger Logger = Log.ForContext(typeof(BackChannelLogoutEndpoint));

    public static async Task HandleAsync(
        HttpContext context,
        IOptionsMonitor<OpenIdConnectOptions> oidcOptions,
        LogoutTokenReplayCache replayCache,
        IBffSessionStore store,
        BffTicketStore ticketStore,
        RefreshTokenRevocation revocation)
    {
        context.Response.Headers.Append("Cache-Control", "no-cache, no-store");
        context.Response.Headers.Append("Pragma", "no-cache");
        var cancellationToken = context.RequestAborted;

        var logoutToken = await ReadLogoutTokenAsync(context.Request);

        if (string.IsNullOrWhiteSpace(logoutToken))
        {
            Logger.Here().Information("Rejected a back-channel logout request with no logout_token");
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var token = await ValidateAsync(logoutToken, oidcOptions, cancellationToken);
        if (token is null)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        // The jti goes into the replay cache only after the BFF sessions ended. A failure on the way then lets the
        // identity provider send the same token again.
        if (await replayCache.IsUsedAsync(token.Jti, cancellationToken))
        {
            Logger.Here().Information("Rejected a replay of the logout token {LogoutTokenId}", Fingerprint.Of(token.Jti));
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var ended = await EndSessionsAsync(token, store, ticketStore, revocation, cancellationToken);

        if (!await replayCache.TryAddAsync(token.Jti, token.Expires, cancellationToken))
        {
            // Another request with the same token came first. It ended the same BFF sessions.
            Logger.Here().Information("Rejected a replay of the logout token {LogoutTokenId}", Fingerprint.Of(token.Jti));
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        Logger.Here().Information(
            "Ended {Count} BFF sessions for the logout token {LogoutTokenId} with sub {Subject} and sid {SessionId}",
            ended, Fingerprint.Of(token.Jti), token.Subject, token.SessionId);
        context.Response.StatusCode = StatusCodes.Status200OK;
    }

    /// <summary>
    /// The <c>logout_token</c> form field, or null when the body is not a form or the form is malformed.
    /// </summary>
    private static async Task<string?> ReadLogoutTokenAsync(HttpRequest request)
    {
        if (!request.HasFormContentType)
            return null;

        try
        {
            return (await request.ReadFormAsync(request.HttpContext.RequestAborted))["logout_token"].FirstOrDefault();
        }
        // The form reader throws these for a malformed form, such as a multipart body with no boundary or a form
        // over the size limits. BadHttpRequestException is an IOException for a body that the server rejects.
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or BadHttpRequestException)
        {
            Logger.Here().Information(ex, "Rejected a back-channel logout request with a malformed form");
            return null;
        }
    }

    /// <summary>
    /// Deletes the rows of the IdP session and revokes their refresh tokens. A revocation error does not change
    /// the result. Returns the number of rows that this request deleted.
    /// </summary>
    private static async Task<int> EndSessionsAsync(LogoutToken token, IBffSessionStore store, BffTicketStore ticketStore,
        RefreshTokenRevocation revocation, CancellationToken cancellationToken)
    {
        var sessions = token.SessionId is { } sessionId
            ? await store.FindBySessionIdAsync(sessionId, cancellationToken)
            : await store.FindBySubjectAsync(token.Subject!, cancellationToken);

        var ended = 0;
        foreach (var session in sessions)
        {
            // Delete the row first, so that a late write of a request that runs now cannot bring it back.
            if (await store.DeleteAsync(session.Key, cancellationToken) is null)
                continue;

            ended++;
            Telemetry.RecordSessionsEnded(SessionEndReason.BackChannelLogout);
            Logger.Here().Information("Deleted the BFF session {SessionKey} for a back-channel logout", Fingerprint.Of(session.Key));

            if (ticketStore.TokensOf(session)?.RefreshToken is { Length: > 0 } refreshToken)
                await revocation.RevokeAsync(refreshToken, cancellationToken);
        }

        return ended;
    }

    /// <summary>
    /// The checks of the logout token (Back-Channel Logout 1.0 section 2.6). Returns null when a check fails.
    /// </summary>
    public static async Task<LogoutToken?> ValidateAsync(
        string logoutToken,
        IOptionsMonitor<OpenIdConnectOptions> oidcOptions,
        CancellationToken cancellationToken)
    {
        var options = oidcOptions.Get(OpenIdConnectDefaults.AuthenticationScheme);
        OpenIdConnectConfiguration? configuration;
        try
        {
            configuration = await OidcDiscovery.GetConfigurationAsync(oidcOptions, cancellationToken);
        }
        // The ConfigurationManager throws when it cannot get the document. The token then cannot pass.
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.Here().Error(ex, "Rejected a logout token, because the discovery document could not be read");
            return null;
        }

        if (configuration is null)
        {
            Logger.Here().Error("Rejected a logout token, because the OIDC handler has no discovery document");
            return null;
        }

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

        var identity = result.ClaimsIdentity;
        return new LogoutToken(
            Jti: identity.FindFirst("jti")!.Value,
            Subject: NullIfBlank(identity.FindFirst("sub")?.Value),
            SessionId: NullIfBlank(identity.FindFirst("sid")?.Value),
            // A token stays valid for the clock skew after its exp, so the replay cache keeps it as long
            Expires: new DateTimeOffset(token.ValidTo, TimeSpan.Zero) + parameters.ClockSkew);
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string? CheckClaims(JsonWebToken token, ClaimsIdentity identity)
    {
        // A typ is optional, but when present it must name a logout token, so that another JWT of the same
        // issuer, such as an ID token, cannot pass as one. Media type names compare case-insensitive.
        if (!string.IsNullOrEmpty(token.Typ)
            && !string.Equals(token.Typ, "logout+jwt", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(token.Typ, "application/logout+jwt", StringComparison.OrdinalIgnoreCase))
            return $"the typ header is {LogValue.Clean(token.Typ)}, not logout+jwt";

        if (identity.FindFirst("iat") is null)
            return "the iat claim is missing";

        // Back-Channel Logout 1.0 section 2.4 requires a jti, and the replay check needs it
        var jti = identity.FindFirst("jti")?.Value;
        if (string.IsNullOrWhiteSpace(jti))
            return "the jti claim is missing";
        if (jti.Length > UsedLogoutTokenConfiguration.JtiMaxLength)
            return $"the jti claim is longer than {UsedLogoutTokenConfiguration.JtiMaxLength} characters";

        if (string.IsNullOrWhiteSpace(identity.FindFirst("sub")?.Value) && string.IsNullOrWhiteSpace(identity.FindFirst("sid")?.Value))
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

/// <summary>
/// A logout token that passed the checks. It has a <see cref="Subject"/>, a <see cref="SessionId"/>, or both.
/// </summary>
/// <param name="Jti">The <c>jti</c> claim, for the replay check.</param>
/// <param name="Subject">The <c>sub</c> claim.</param>
/// <param name="SessionId">The <c>sid</c> claim.</param>
/// <param name="Expires">The time after which the token cannot pass the checks again.</param>
public sealed record LogoutToken(string Jti, string? Subject, string? SessionId, DateTimeOffset Expires);
