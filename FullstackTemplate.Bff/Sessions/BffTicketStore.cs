using FullstackTemplate.Bff.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Serilog;
using ILogger = Serilog.ILogger;

namespace FullstackTemplate.Bff.Sessions;

/// <summary>
/// The <see cref="CookieAuthenticationOptions.SessionStore"/> of the cookie handler. The cookie then holds only the
/// key of the BFF session, and the ticket with the tokens is in the <see cref="IBffSessionStore"/>.
/// <para>
/// The rules of the writes:
/// </para>
/// <list type="bullet">
///   <item>A read keeps the version of the row for the rest of the request (<see cref="BffSessionRequest"/>).</item>
///   <item>Each write is conditional on that version, and never creates a row. A write that succeeds moves the
///   version forward.</item>
///   <item>A renewal that changes only the expiry writes only the expiry, so it never replaces newer tokens. If it
///   loses the race, it reads the row again and applies only its expiry.</item>
///   <item>A write never moves the renewal time or the expiry of the row back. It keeps the later of its own value
///   and the value in the row, also when another request wrote the row after this request read it.</item>
///   <item>A write that loses the race <see cref="WriteAttempts"/> times stops and logs the failure. A token write
///   that stops this way is dropped, and its new refresh token is revoked, because no BFF session holds it.</item>
///   <item>A write with new tokens comes from a refresh, inside the refresh lock (<see cref="RefreshLock"/>). It can
///   lose the race only to an expiry renewal. A conflict with other tokens is a fault, and throws.</item>
///   <item>If a write with new tokens finds no row, the BFF session ended during the request. The new refresh token
///   is then revoked, because no BFF session holds it.</item>
///   <item>A refresh response with no <c>id_token</c> keeps the stored <c>id_token</c>.</item>
///   <item>A renewal never moves the expiry past the absolute limit.</item>
/// </list>
/// </summary>
public sealed class BffTicketStore(
    IDataProtectionProvider dataProtectionProvider,
    IOptionsMonitor<BffSessionOptions> options,
    TimeProvider timeProvider) : ITicketStore
{
    /// <summary>
    /// A write tries at most this many times when other writes change the row at the same time.
    /// </summary>
    private const int WriteAttempts = 3;

    private static readonly ILogger Logger = Log.ForContext<BffTicketStore>();

    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("FullstackTemplate.Bff.Sessions.Ticket");

    public async Task<string> StoreAsync(AuthenticationTicket ticket, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var (store, request) = Services(httpContext);
        var now = timeProvider.GetUtcNow();
        var settings = options.CurrentValue;

        var subjectId = ticket.Principal.FindFirst("sub")?.Value
            ?? throw new InvalidOperationException("A BFF session needs a sub claim.");
        var absoluteExpires = now + settings.AbsoluteLifetime;
        var session = new BffSession(
            Key: SessionKey.Create(),
            SubjectId: subjectId,
            SessionId: ticket.Principal.FindFirst("sid")?.Value,
            Created: now,
            Renewed: ticket.Properties.IssuedUtc ?? now,
            Expires: Earlier(ticket.Properties.ExpiresUtc ?? now + settings.SlidingExpiration, absoluteExpires),
            AbsoluteExpires: absoluteExpires,
            Ticket: Protect(ticket),
            Version: 0);

        var created = await store.CreateAsync(session, cancellationToken);
        Telemetry.SessionsStarted.Add(1);
        request.Remember(created, SessionTokens.From(ticket.Properties));
        Logger.Here().Information("Created the BFF session {SessionKey}", Fingerprint.Of(created.Key));
        return created.Key;
    }

    public async Task<AuthenticationTicket?> RetrieveAsync(string key, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var (store, request) = Services(httpContext);
        var session = await store.GetAsync(key, cancellationToken);
        if (session is null)
        {
            request.Forget();
            return null;
        }

        var ticket = Unprotect(session);
        if (ticket is null)
        {
            request.Forget();
            return null;
        }

        // The row is the source of the times. The cookie handler reads them for its expiry and sliding checks, and
        // /bff/user reads ExpiresUtc for bff:session_expires_in.
        ticket.Properties.IssuedUtc = session.Renewed;
        ticket.Properties.ExpiresUtc = session.EndsAt;

        request.Remember(session, SessionTokens.From(ticket.Properties));
        return ticket;
    }

    public async Task RenewAsync(string key, AuthenticationTicket ticket, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var (store, request) = Services(httpContext);
        if (request.Key != key)
        {
            // The cookie handler renews only a key that it read in this request.
            Logger.Here().Warning("Skipped the renewal of the BFF session {SessionKey}, because the request did not read it",
                Fingerprint.Of(key));
            return;
        }

        var now = timeProvider.GetUtcNow();
        var renewed = Later(ticket.Properties.IssuedUtc ?? now, request.Renewed);
        var expires = Later(
            Earlier(ticket.Properties.ExpiresUtc ?? now + options.CurrentValue.SlidingExpiration, request.AbsoluteExpires),
            request.Expires);

        KeepStoredIdToken(ticket.Properties, request.Tokens);
        var tokens = SessionTokens.From(ticket.Properties);

        if (tokens == request.Tokens)
            await RenewExpiryAsync(store, request, key, renewed, expires, cancellationToken);
        else
            await RenewTokensAsync(store, request, httpContext, key, ticket, tokens, renewed, expires);
    }

    public async Task RemoveAsync(string key, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var (store, request) = Services(httpContext);
        if (await store.DeleteAsync(key, cancellationToken) is not null)
            Logger.Here().Information("Deleted the BFF session {SessionKey}", Fingerprint.Of(key));
        request.Forget();
    }

    private static async Task RenewExpiryAsync(IBffSessionStore store, BffSessionRequest request, string key,
        DateTimeOffset renewed, DateTimeOffset expires, CancellationToken cancellationToken)
    {
        var version = request.Version;
        for (var attempt = 0; attempt < WriteAttempts; attempt++)
        {
            switch (await store.RenewExpiryAsync(key, renewed, expires, version, cancellationToken))
            {
                case BffSessionWrite.Written written:
                    request.Written(written.Version, renewed, expires);
                    return;
                case BffSessionWrite.Missing:
                    request.Forget();
                    return;
                case BffSessionWrite.Conflict conflict when conflict.Current.Expires >= expires:
                    // Another request moved the expiry as far or farther. There is nothing to write.
                    return;
                case BffSessionWrite.Conflict conflict:
                    // Another request wrote the row, maybe with newer tokens. Keep them, and apply only the expiry.
                    version = conflict.Current.Version;
                    renewed = Later(renewed, conflict.Current.Renewed);
                    break;
            }
        }

        Logger.Here().Warning("Skipped the expiry renewal of the BFF session {SessionKey} after {Attempts} conflicts",
            Fingerprint.Of(key), WriteAttempts);
    }

    /// <summary>
    /// Writes the tokens of a refresh. The write does not stop when the client goes away, because the identity
    /// provider has rotated the refresh token already, and other requests can wait for this refresh.
    /// </summary>
    private async Task RenewTokensAsync(IBffSessionStore store, BffSessionRequest request, HttpContext httpContext,
        string key, AuthenticationTicket ticket, SessionTokens tokens, DateTimeOffset renewed, DateTimeOffset expires)
    {
        var protectedTicket = Protect(ticket);
        var version = request.Version;
        for (var attempt = 0; attempt < WriteAttempts; attempt++)
        {
            switch (await store.RenewAsync(key, protectedTicket, renewed, expires, version, CancellationToken.None))
            {
                case BffSessionWrite.Written written:
                    request.Written(written.Version, renewed, expires, tokens);
                    return;

                case BffSessionWrite.Missing:
                    var startRefreshToken = request.Tokens.RefreshToken;
                    request.Forget();
                    Logger.Here().Information("Dropped the token write of the BFF session {SessionKey}, because the BFF session ended",
                        Fingerprint.Of(key));
                    await RevokeNewRefreshTokenAsync(httpContext, tokens, startRefreshToken, CancellationToken.None);
                    return;

                case BffSessionWrite.Conflict conflict when TokensOf(conflict.Current) == request.Tokens:
                    // The other write changed only the expiry. The tokens of this request are still the newest. Keep
                    // the later times, so that the write does not move the expiry of the other write back.
                    version = conflict.Current.Version;
                    renewed = Later(renewed, conflict.Current.Renewed);
                    expires = Later(expires, conflict.Current.Expires);
                    break;

                case BffSessionWrite.Conflict:
                    // Each token write runs inside the refresh lock, with the version that the lock read, so no
                    // other token write can come between. A conflict with other tokens is a fault.
                    throw new InvalidOperationException(
                        $"The token write of the BFF session {Fingerprint.Of(key)} found other tokens. Each token write must run inside the refresh lock.");
            }
        }

        // The row keeps the tokens that the request read. If the identity provider rotated the refresh token, the
        // next refresh fails and ends the BFF session under the refresh failure rules.
        Logger.Here().Error("Dropped the token write of the BFF session {SessionKey} after {Attempts} conflicts",
            Fingerprint.Of(key), WriteAttempts);
        await RevokeNewRefreshTokenAsync(httpContext, tokens, request.Tokens.RefreshToken, CancellationToken.None);
    }

    /// <summary>
    /// Revokes the refresh token of a dropped token write, if a refresh in this request got a new one. No BFF
    /// session holds it.
    /// </summary>
    private static async Task RevokeNewRefreshTokenAsync(HttpContext httpContext, SessionTokens tokens,
        string? startRefreshToken, CancellationToken cancellationToken)
    {
        if (tokens.RefreshToken is { } newRefreshToken && newRefreshToken != startRefreshToken)
            await httpContext.RequestServices.GetRequiredService<RefreshTokenRevocation>()
                .RevokeAsync(newRefreshToken, cancellationToken);
    }

    /// <summary>
    /// Token management writes a null <c>id_token</c> when a refresh response has none. Logout needs the
    /// <c>id_token</c> for <c>id_token_hint</c>, so keep the stored one.
    /// </summary>
    private static void KeepStoredIdToken(AuthenticationProperties properties, SessionTokens stored)
    {
        if (!string.IsNullOrEmpty(properties.GetTokenValue("id_token")) || string.IsNullOrEmpty(stored.IdToken))
            return;

        var tokens = properties.GetTokens().Where(t => t.Name != "id_token").ToList();
        tokens.Add(new AuthenticationToken { Name = "id_token", Value = stored.IdToken });
        properties.StoreTokens(tokens);
    }

    /// <summary>
    /// The refresh token in the ticket of <paramref name="session"/>, or null if it has none or if the ticket cannot
    /// be read.
    /// </summary>
    public string? RefreshTokenOf(BffSession session) => TokensOf(session)?.RefreshToken;

    /// <summary>
    /// The tokens in the ticket of <paramref name="session"/>, or null if the ticket cannot be read.
    /// </summary>
    public SessionTokens? TokensOf(BffSession session) =>
        TicketOf(session) is { } ticket ? SessionTokens.From(ticket.Properties) : null;

    /// <summary>
    /// The ticket of <paramref name="session"/>, or null if it cannot be read.
    /// </summary>
    public AuthenticationTicket? TicketOf(BffSession session) => Unprotect(session);

    private byte[] Protect(AuthenticationTicket ticket) => _protector.Protect(TicketSerializer.Default.Serialize(ticket));

    private AuthenticationTicket? Unprotect(BffSession session)
    {
        try
        {
            return TicketSerializer.Default.Deserialize(_protector.Unprotect(session.Ticket));
        }
        catch (System.Security.Cryptography.CryptographicException ex)
        {
            // For example, the Data Protection key of the ticket is gone.
            Logger.Here().Warning(ex, "Could not read the ticket of the BFF session {SessionKey}", Fingerprint.Of(session.Key));
            return null;
        }
    }

    private static DateTimeOffset Earlier(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;

    private static DateTimeOffset Later(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;

    private static (IBffSessionStore Store, BffSessionRequest Request) Services(HttpContext httpContext) =>
        (httpContext.RequestServices.GetRequiredService<IBffSessionStore>(),
            httpContext.RequestServices.GetRequiredService<BffSessionRequest>());

    // The cookie handler calls only the overloads with the HttpContext.
    public Task<string> StoreAsync(AuthenticationTicket ticket) => throw HttpContextRequired();

    public Task RenewAsync(string key, AuthenticationTicket ticket) => throw HttpContextRequired();

    public Task<AuthenticationTicket?> RetrieveAsync(string key) => throw HttpContextRequired();

    public Task RemoveAsync(string key) => throw HttpContextRequired();

    private static NotSupportedException HttpContextRequired() =>
        new("The BFF session store needs the HttpContext of the request.");
}
