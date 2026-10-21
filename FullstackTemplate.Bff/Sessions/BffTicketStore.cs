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
    // A renewal of the expiry retries at most this many times when other writes change the row at the same time.
    private const int ExpiryRenewalAttempts = 3;

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
        var renewed = ticket.Properties.IssuedUtc ?? now;
        var expires = Earlier(ticket.Properties.ExpiresUtc ?? now + options.CurrentValue.SlidingExpiration, request.AbsoluteExpires);

        KeepStoredIdToken(ticket.Properties, request.Tokens);
        var tokens = SessionTokens.From(ticket.Properties);

        if (tokens == request.Tokens)
            await RenewExpiryAsync(store, request, key, renewed, expires, cancellationToken);
        else
            await RenewTokensAsync(store, request, httpContext, key, ticket, tokens, renewed, expires, cancellationToken);
    }

    public async Task RemoveAsync(string key, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var (store, request) = Services(httpContext);
        if (await store.DeleteAsync(key, cancellationToken))
            Logger.Here().Information("Deleted the BFF session {SessionKey}", Fingerprint.Of(key));
        request.Forget();
    }

    private static async Task RenewExpiryAsync(IBffSessionStore store, BffSessionRequest request, string key,
        DateTimeOffset renewed, DateTimeOffset expires, CancellationToken cancellationToken)
    {
        var version = request.Version;
        for (var attempt = 0; attempt < ExpiryRenewalAttempts; attempt++)
        {
            switch (await store.RenewExpiryAsync(key, renewed, expires, version, cancellationToken))
            {
                case BffSessionWrite.Written written:
                    request.Written(written.Version);
                    return;
                case BffSessionWrite.Missing:
                    request.Forget();
                    return;
                case BffSessionWrite.Conflict conflict:
                    // Another request wrote the row, maybe with newer tokens. Keep them, and apply only the expiry.
                    version = conflict.Current.Version;
                    break;
            }
        }

        Logger.Here().Warning("Skipped the expiry renewal of the BFF session {SessionKey} after {Attempts} conflicts",
            Fingerprint.Of(key), ExpiryRenewalAttempts);
    }

    private async Task RenewTokensAsync(IBffSessionStore store, BffSessionRequest request, HttpContext httpContext,
        string key, AuthenticationTicket ticket, SessionTokens tokens, DateTimeOffset renewed, DateTimeOffset expires,
        CancellationToken cancellationToken)
    {
        var protectedTicket = Protect(ticket);
        var version = request.Version;
        while (true)
        {
            switch (await store.RenewAsync(key, protectedTicket, renewed, expires, version, cancellationToken))
            {
                case BffSessionWrite.Written written:
                    request.Written(written.Version, tokens);
                    return;

                case BffSessionWrite.Missing:
                    var startRefreshToken = request.Tokens.RefreshToken;
                    request.Forget();
                    Logger.Here().Information("Dropped the token write of the BFF session {SessionKey}, because the BFF session ended",
                        Fingerprint.Of(key));
                    if (tokens.RefreshToken is { } newRefreshToken && newRefreshToken != startRefreshToken)
                        await httpContext.RequestServices.GetRequiredService<RefreshTokenRevocation>()
                            .RevokeAsync(newRefreshToken, cancellationToken);
                    return;

                case BffSessionWrite.Conflict conflict when TokensOf(conflict.Current) == request.Tokens:
                    // The other write changed only the expiry. The tokens of this request are still the newest.
                    version = conflict.Current.Version;
                    break;

                case BffSessionWrite.Conflict:
                    // Another request wrote other tokens. The refresh lock of a later slice makes this case rare. The
                    // row keeps the tokens of the other request.
                    Logger.Here().Warning("Dropped the token write of the BFF session {SessionKey}, because another request wrote newer tokens",
                        Fingerprint.Of(key));
                    return;
            }
        }
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

    private SessionTokens? TokensOf(BffSession session) =>
        Unprotect(session) is { } ticket ? SessionTokens.From(ticket.Properties) : null;

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
