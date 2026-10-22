using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.OpenIdConnect;
using FullstackTemplate.Bff.Sessions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Npgsql;
using Serilog;
using ILogger = Serilog.ILogger;

namespace FullstackTemplate.Bff.Authentication;

/// <summary>
/// The refresh lock: one token refresh at a time for each BFF session, across all BFF instances. It replaces the
/// default <see cref="IUserTokenRequestConcurrencyControl"/> of token management, and it has two layers.
/// <list type="number">
///   <item>In process: the requests that need a refresh with the same refresh token share one refresh, so that
///   one instance takes the database lock only one time for each refresh. The first request runs it. Each other
///   request waits at most <see cref="RefreshOptions.LockTimeout"/>, and stops its wait when its own client goes
///   away.</item>
///   <item>Across instances: a <c>bffdb</c> transaction takes <c>pg_advisory_xact_lock</c> on a 64-bit hash of
///   the BFF session key, and then reads the row again. If another request already refreshed, the tokens in the
///   row are the result, and the identity provider gets no call. If not, the refresh and the write of its tokens
///   run inside the lock, with the <c>xmin</c> of the row that the lock read.</item>
/// </list>
/// <para>
/// The lock, the reread, and the write use the scoped <see cref="BffDbContext"/> of the request, so the write is
/// inside the lock transaction. The lock releases itself when the transaction ends, also if the instance stops.
/// A refresh that fails with <c>invalid_grant</c> deletes the row inside the lock, so that a waiter reads no row
/// and does not send the rejected refresh token again.
/// </para>
/// <para>
/// The shared refresh and its commit do not stop when a client goes away: the identity provider can have rotated
/// the refresh token already, and a rollback then leaves a used refresh token in the row. For this reason, the
/// caller must not give <c>RequestAborted</c> to token management, because token management gives its token to
/// the shared refresh too. The request that runs the refresh waits for its end, because the refresh uses the
/// scoped services of that request. <see cref="RefreshOptions.TokenRequestTimeout"/> limits the call to the
/// identity provider.
/// </para>
/// </summary>
public sealed class RefreshLock(
    IHttpContextAccessor httpContextAccessor,
    BffTicketStore ticketStore,
    IStoreTokensInAuthenticationProperties tokensInProperties,
    IOptionsMonitor<RefreshOptions> options) : IUserTokenRequestConcurrencyControl
{
    /// <summary>
    /// The error of a refresh that did not get the lock within <see cref="RefreshOptions.LockTimeout"/>. It is a
    /// temporary failure.
    /// </summary>
    public const string LockTimeoutError = "bff_refresh_lock_timeout";

    /// <summary>
    /// The error of a refresh whose BFF session ended before the lock read the row, for example because a refresh
    /// on another instance got <c>invalid_grant</c>, or because of a logout.
    /// </summary>
    public const string SessionEndedError = "bff_session_ended";

    /// <summary>
    /// The error of a database retry after the refresh call. The identity provider can have used up the refresh
    /// token, so the retry does not send it again. It is a temporary failure.
    /// </summary>
    public const string RetryAfterRefreshError = "bff_refresh_retry";

    private static readonly ILogger Logger = Log.ForContext<RefreshLock>();

    private readonly ConcurrentDictionary<string, TaskCompletionSource<TokenResult<UserToken>>> _inFlight = new(StringComparer.Ordinal);

    private int _waiting;

    /// <summary>
    /// The number of requests in this instance that wait now for the refresh of another request.
    /// </summary>
    public int WaitingRequests => Volatile.Read(ref _waiting);

    /// <summary>
    /// Runs <paramref name="tokenRetriever"/> one time for all the requests with the same refresh token. It does not
    /// use <paramref name="ct"/>: a waiter stops at the <c>RequestAborted</c> of its own request.
    /// </summary>
    public async Task<TokenResult<UserToken>> ExecuteWithConcurrencyControlAsync(UserRefreshToken key,
        Func<Task<TokenResult<UserToken>>> tokenRetriever, CancellationToken ct = default)
    {
        var refreshToken = key.RefreshToken.ToString();
        var own = new TaskCompletionSource<TokenResult<UserToken>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var refresh = _inFlight.GetOrAdd(refreshToken, own);
        if (!ReferenceEquals(refresh, own))
            return await WaitForRefreshAsync(refresh.Task);

        // This request runs the refresh, in its own scope and HttpContext.
        try
        {
            var result = await RefreshInLockAsync(tokenRetriever);
            own.SetResult(result);
            return result;
        }
        catch (Exception ex)
        {
            own.SetException(ex);
            // Observed here, so that a refresh with no waiters does not raise UnobservedTaskException.
            _ = own.Task.Exception;
            throw;
        }
        finally
        {
            // Only this refresh. A later refresh with the same token can have its own entry by now.
            _inFlight.TryRemove(new KeyValuePair<string, TaskCompletionSource<TokenResult<UserToken>>>(refreshToken, own));
        }
    }

    /// <summary>
    /// The key of the advisory lock of the BFF session <paramref name="sessionKey"/>: the first 8 bytes of the
    /// SHA-256 of the key.
    /// </summary>
    public static long LockIdOf(string sessionKey) =>
        BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes(sessionKey)));

    /// <summary>
    /// True when <paramref name="error"/> ends the BFF session: the identity provider rejected the refresh token
    /// (<c>invalid_grant</c>), or the BFF session ended during the wait. Each other failure is temporary.
    /// </summary>
    public static bool EndsSession(string? error) =>
        RefreshFailureMetrics.KindOf(error) == RefreshFailureKind.Permanent
        || string.Equals(error, SessionEndedError, StringComparison.Ordinal);

    /// <summary>
    /// Waits for the refresh of another request in this instance, at most <see cref="RefreshOptions.LockTimeout"/>,
    /// as a wait for the lock of another instance does. The wait stops when the client of this request goes away.
    /// </summary>
    private async Task<TokenResult<UserToken>> WaitForRefreshAsync(Task<TokenResult<UserToken>> refresh)
    {
        var httpContext = CurrentHttpContext();
        var lockTimeout = options.CurrentValue.LockTimeout;
        Interlocked.Increment(ref _waiting);
        try
        {
            return await refresh.WaitAsync(lockTimeout, httpContext.RequestAborted);
        }
        catch (TimeoutException)
        {
            Telemetry.RecordRefreshFailure(RefreshFailureKind.Temporary);
            Logger.Here().Warning("Kept the BFF session {SessionKey} with no refresh, because the refresh of another request did not end within {LockTimeout}",
                Fingerprint.Of(httpContext.RequestServices.GetRequiredService<BffSessionRequest>().Key ?? ""), lockTimeout);
            return TokenResult.Failure(LockTimeoutError);
        }
        finally
        {
            Interlocked.Decrement(ref _waiting);
        }
    }

    private HttpContext CurrentHttpContext() => httpContextAccessor.HttpContext
        ?? throw new InvalidOperationException("The refresh lock needs the HttpContext of the request.");

    private async Task<TokenResult<UserToken>> RefreshInLockAsync(Func<Task<TokenResult<UserToken>>> tokenRetriever)
    {
        var services = CurrentHttpContext().RequestServices;
        var request = services.GetRequiredService<BffSessionRequest>();
        if (request.Key is not { } sessionKey)
            return TokenResult.Failure(SessionEndedError);

        var dbContext = services.GetRequiredService<BffDbContext>();
        var store = services.GetRequiredService<IBffSessionStore>();
        var startTokens = request.Tokens;
        var lockTimeout = options.CurrentValue.LockTimeout;
        var refreshCalled = false;

        // The Aspire integration turns on EF retries, and a retrying strategy runs a user transaction only as one
        // unit. A retry starts again with the lock and the reread. No cancellation: see the remarks of the class.
        var outcome = await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            async cancellation =>
            {
                if (refreshCalled)
                    return Outcome.RetryAfterRefresh;

                await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellation);
                if (!await TryLockAsync(dbContext, transaction, sessionKey, lockTimeout, cancellation))
                    return Outcome.LockTimeout;

                var row = await store.GetAsync(sessionKey, cancellation);
                if (row is null || ticketStore.TicketOf(row) is not { } ticket)
                    return Outcome.SessionEnded;

                var storedTokens = SessionTokens.From(ticket.Properties);
                if (storedTokens != startTokens)
                {
                    // Another request refreshed after this request read the row. Its tokens are in the row. This
                    // request keeps the version and the tokens that it read, so that a later expiry renewal in it
                    // writes only the expiry.
                    await transaction.CommitAsync(cancellation);
                    return new Outcome(OutcomeKind.StoredTokens, StoredToken(ticket.Properties), Ended: false);
                }

                // The token write of the refresh is conditional on the xmin of the row that the lock read.
                request.Remember(row, storedTokens);
                refreshCalled = true;
                var result = await tokenRetriever();

                var ended = !result.WasSuccessful(out _, out var failure)
                            && RefreshFailureMetrics.KindOf(failure.Error) == RefreshFailureKind.Permanent
                            && await store.DeleteAsync(sessionKey, cancellation) is not null;

                await transaction.CommitAsync(cancellation);
                return new Outcome(OutcomeKind.Refreshed, result, ended);
            },
            CancellationToken.None);

        Report(outcome, sessionKey, lockTimeout);
        return outcome.Result;
    }

    private static void Report(Outcome outcome, string sessionKey, TimeSpan lockTimeout)
    {
        var fingerprint = Fingerprint.Of(sessionKey);
        switch (outcome.Kind)
        {
            case OutcomeKind.LockTimeout:
                Telemetry.RecordRefreshFailure(RefreshFailureKind.Temporary);
                Logger.Here().Warning("Kept the BFF session {SessionKey} with no refresh, because its refresh lock was not free within {LockTimeout}",
                    fingerprint, lockTimeout);
                break;

            case OutcomeKind.RetryAfterRefresh:
                Telemetry.RecordRefreshFailure(RefreshFailureKind.Temporary);
                Logger.Here().Warning("Kept the BFF session {SessionKey} with no refresh, because the database failed after the refresh call",
                    fingerprint);
                break;

            case OutcomeKind.SessionEnded:
                Logger.Here().Information("Skipped the refresh of the BFF session {SessionKey}, because the BFF session ended",
                    fingerprint);
                break;

            case OutcomeKind.StoredTokens:
                Logger.Here().Debug("Used the tokens that another request refreshed for the BFF session {SessionKey}", fingerprint);
                break;

            case OutcomeKind.Refreshed:
                ReportRefresh(outcome, fingerprint);
                break;
        }
    }

    private static void ReportRefresh(Outcome outcome, string fingerprint)
    {
        if (outcome.Result.WasSuccessful(out _, out var failure))
        {
            Logger.Here().Debug("Refreshed the tokens of the BFF session {SessionKey}", fingerprint);
            return;
        }

        if (RefreshFailureMetrics.KindOf(failure.Error) == RefreshFailureKind.Temporary)
        {
            Logger.Here().Warning("Kept the BFF session {SessionKey} after a temporary refresh failure: {Error} {ErrorDescription}",
                fingerprint, failure.Error, failure.ErrorDescription);
            return;
        }

        // An expected end: the refresh token expired or was revoked, or the IdP session ended. Not a fault of the BFF.
        if (outcome.Ended)
            Telemetry.RecordSessionsEnded(SessionEndReason.RefreshFailure);
        Logger.Here().Information("Ended the BFF session {SessionKey}, because the identity provider rejected its refresh token with {Error}",
            fingerprint, failure.Error);
    }

    /// <summary>
    /// Takes the advisory lock of the BFF session in <paramref name="transaction"/>. Returns false when the wait
    /// reaches <paramref name="lockTimeout"/>. The commands go straight to the connection of the transaction, so
    /// that EF Core does not log the expected lock timeout as a failed command. The <c>lock_timeout</c> setting
    /// stays for the rest of the transaction, so that no later lock wait in it is longer.
    /// </summary>
    private static async Task<bool> TryLockAsync(BffDbContext dbContext, IDbContextTransaction transaction,
        string sessionKey, TimeSpan lockTimeout, CancellationToken ct)
    {
        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        // Two statements, because Postgres does not promise the order in which it runs the parts of one SELECT
        command.CommandText = "SELECT set_config('lock_timeout', @timeout, true); SELECT pg_advisory_xact_lock(@lockId)";
        AddParameter(command, "timeout", $"{(long)lockTimeout.TotalMilliseconds}ms");
        AddParameter(command, "lockId", LockIdOf(sessionKey));

        try
        {
            await command.ExecuteNonQueryAsync(ct);
            return true;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.LockNotAvailable)
        {
            return false;
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private TokenResult<UserToken> StoredToken(AuthenticationProperties properties) =>
        tokensInProperties.GetUserToken(properties).WasSuccessful(out var stored, out var failure)
            ? stored.TokenForSpecifiedParameters is { } token ? token : TokenResult.Failure("The BFF session has no access token")
            : failure;

    private enum OutcomeKind
    {
        LockTimeout,
        RetryAfterRefresh,
        SessionEnded,
        StoredTokens,
        Refreshed,
    }

    /// <param name="Kind">What happened inside the lock.</param>
    /// <param name="Result">The result for token management.</param>
    /// <param name="Ended">True when this refresh deleted the row after a permanent failure.</param>
    private sealed record Outcome(OutcomeKind Kind, TokenResult<UserToken> Result, bool Ended)
    {
        public static readonly Outcome LockTimeout = new(OutcomeKind.LockTimeout, TokenResult.Failure(LockTimeoutError), false);

        public static readonly Outcome RetryAfterRefresh = new(OutcomeKind.RetryAfterRefresh, TokenResult.Failure(RetryAfterRefreshError), false);

        public static readonly Outcome SessionEnded = new(OutcomeKind.SessionEnded, TokenResult.Failure(SessionEndedError), false);
    }
}
