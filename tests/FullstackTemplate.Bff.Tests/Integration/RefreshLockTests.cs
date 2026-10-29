namespace FullstackTemplate.Bff.Tests.Integration;

using System.Net;
using Duende.AccessTokenManagement.OpenIdConnect;
using FullstackTemplate.Bff.Authentication;
using FullstackTemplate.Bff.Sessions;
using FullstackTemplate.Bff.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using NpgsqlTypes;
using Shouldly;

/// <summary>
/// The refresh lock of section 3.7 of the spec, with two BFF hosts on one <c>bffdb</c> and a fake token endpoint
/// with rotation.
/// </summary>
public class RefreshLockTests
{
    private const int CallsPerHost = 4;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    // Longer than Timeout, so that only the fake clock moves the refresh timers during a test, and a timer on the
    // real clock fails the test
    private static readonly TimeSpan ClockedLockTimeout = TimeSpan.FromSeconds(25);
    internal static readonly TimeSpan ClockedTokenRequestTimeout = TimeSpan.FromSeconds(20);

    [Test]
    public async Task parallel_calls_on_two_hosts_make_one_refresh_and_all_get_the_new_token()
    {
        // Arrange
        var app = BffApp.Instance;
        var provider = app.IdentityProvider;
        var subject = NewSubject();
        await using var other = BffApp.StartAnotherInstance();
        var (cookie, key) = await SignInAsync(app, $"test_sub={subject}&test_expires_in=60&test_refresh_expires_in=3600");
        var gate = provider.HoldRefreshes(subject);

        using var first = Cookieless(app);
        using var second = Cookieless(other);

        // Act
        var calls = Enumerable.Range(0, CallsPerHost)
            .SelectMany(_ => new[] { first, second })
            .Select(client => client.SendAsync(ApiRequest(cookie)))
            .ToList();

        await gate.Arrived.Task.WaitAsync(Timeout);
        // One host holds the lock and waits for the token endpoint. The other host waits for the lock.
        await WaitForLockWaiterAsync(key);
        gate.Released.SetResult();
        var responses = await Task.WhenAll(calls).WaitAsync(Timeout);

        // Assert
        provider.RefreshCount(subject).ShouldBe(1);
        var issued = provider.IssuedAccessTokens(subject);
        issued.Count.ShouldBe(2);
        var newAccessToken = issued[1];
        foreach (var response in responses)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await response.ReadEchoAsync()).Headers["Authorization"].ShouldBe($"Bearer {newAccessToken}");
        }

        var tokens = await StoredTokensAsync(app, key);
        tokens.AccessToken.ShouldBe(newAccessToken);
        tokens.RefreshToken.ShouldBe(provider.IssuedRefreshTokens(subject)[1]);
    }

    [Test]
    public async Task a_lock_wait_that_reaches_the_limit_keeps_the_session_and_sends_the_current_token()
    {
        // Arrange
        await using var app = BffApp.StartAnotherInstance(new Dictionary<string, string?>
        {
            ["Bff:Refresh:LockTimeout"] = "00:00:00.300",
            ["Bff:Refresh:TokenRequestTimeout"] = "00:00:00.200",
        });
        var provider = app.IdentityProvider;
        var subject = NewSubject();
        var (cookie, key) = await SignInAsync(app, $"test_sub={subject}&test_expires_in=60");
        await using var holder = await HoldLockAsync(key);
        using var browser = Cookieless(app);

        // Act
        var response = await browser.SendAsync(ApiRequest(cookie));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadEchoAsync()).Headers["Authorization"].ShouldBe($"Bearer {provider.IssuedAccessTokens(subject).Single()}");
        provider.RefreshCount(subject).ShouldBe(0);
        (await app.WithStoreAsync(store => store.GetAsync(key, CancellationToken.None))).ShouldNotBeNull();
        BffApp.SessionCookieValue(response).ShouldBeNull();
    }

    [Test]
    public async Task an_invalid_grant_on_one_host_ends_the_session_and_the_waiter_on_the_other_host_sends_no_second_refresh()
    {
        // Arrange
        var app = BffApp.Instance;
        var provider = app.IdentityProvider;
        var subject = NewSubject();
        await using var other = BffApp.StartAnotherInstance();
        var (cookie, key) = await SignInAsync(app, $"test_sub={subject}&test_expires_in=60&test_refresh=fail");
        var gate = provider.HoldRefreshes(subject);
        using var first = Cookieless(app);
        using var second = Cookieless(other);

        // Act
        var firstCall = first.SendAsync(ApiRequest(cookie));
        await gate.Arrived.Task.WaitAsync(Timeout);
        var secondCall = second.SendAsync(ApiRequest(cookie));
        await WaitForLockWaiterAsync(key);
        gate.Released.SetResult();
        var responses = await Task.WhenAll(firstCall, secondCall).WaitAsync(Timeout);

        // Assert
        provider.RefreshCount(subject).ShouldBe(1);
        foreach (var response in responses)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await response.ReadEchoAsync()).Headers.ShouldNotContainKey("Authorization");
            BffApp.SessionCookieValue(response).ShouldBe("");
        }

        (await app.WithStoreAsync(store => store.GetAsync(key, CancellationToken.None))).ShouldBeNull();
    }

    [Test]
    public async Task two_calls_on_one_host_share_one_refresh_in_process()
    {
        // Arrange
        await using var app = BffApp.StartAnotherInstance();
        var provider = app.IdentityProvider;
        var subject = NewSubject();
        var (cookie, key) = await SignInAsync(app, $"test_sub={subject}&test_expires_in=60&test_refresh_expires_in=3600");
        var gate = provider.HoldRefreshes(subject);
        using var browser = Cookieless(app);

        // Act
        var firstCall = browser.SendAsync(ApiRequest(cookie));
        await gate.Arrived.Task.WaitAsync(Timeout);
        var secondCall = browser.SendAsync(ApiRequest(cookie));
        // The second call waits for the refresh of the first in process, not for the advisory lock.
        await WaitForInProcessWaiterAsync(app);
        (await LockCountAsync(key, granted: false)).ShouldBe(0);
        gate.Released.SetResult();
        var responses = await Task.WhenAll(firstCall, secondCall).WaitAsync(Timeout);

        // Assert
        provider.RefreshCount(subject).ShouldBe(1);
        var newAccessToken = provider.IssuedAccessTokens(subject)[1];
        foreach (var response in responses)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await response.ReadEchoAsync()).Headers["Authorization"].ShouldBe($"Bearer {newAccessToken}");
        }

        (await StoredTokensAsync(app, key)).AccessToken.ShouldBe(newAccessToken);
    }

    [Test]
    public async Task an_abort_of_the_call_that_runs_the_refresh_does_not_stop_the_refresh_of_its_waiter()
    {
        // Arrange
        await using var app = BffApp.StartAnotherInstance();
        var provider = app.IdentityProvider;
        var subject = NewSubject();
        var (cookie, key) = await SignInAsync(app, $"test_sub={subject}&test_expires_in=60&test_refresh_expires_in=3600");
        var gate = provider.HoldRefreshes(subject);
        using var browser = Cookieless(app);
        using var abort = new CancellationTokenSource();

        // Act
        var firstCall = browser.SendAsync(ApiRequest(cookie), abort.Token);
        await gate.Arrived.Task.WaitAsync(Timeout);
        var secondCall = browser.SendAsync(ApiRequest(cookie));
        await WaitForInProcessWaiterAsync(app);
        // The test server cancels RequestAborted at once, and ends the client call when the request ends.
        await abort.CancelAsync();
        gate.Released.SetResult();
        var response = await secondCall.WaitAsync(Timeout);

        // Assert
        await Should.ThrowAsync<OperationCanceledException>(firstCall.WaitAsync(Timeout));

        provider.RefreshCount(subject).ShouldBe(1);
        var newAccessToken = provider.IssuedAccessTokens(subject)[1];
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadEchoAsync()).Headers["Authorization"].ShouldBe($"Bearer {newAccessToken}");
        var tokens = await StoredTokensAsync(app, key);
        tokens.AccessToken.ShouldBe(newAccessToken);
        tokens.RefreshToken.ShouldBe(provider.IssuedRefreshTokens(subject)[1]);
    }

    [Test]
    public async Task an_abort_after_the_identity_provider_rotated_the_refresh_token_still_writes_the_new_tokens()
    {
        // Arrange
        await using var app = BffApp.StartAnotherInstance();
        var provider = app.IdentityProvider;
        var subject = NewSubject();
        var (cookie, key) = await SignInAsync(app, $"test_sub={subject}&test_expires_in=60&test_refresh_expires_in=3600");
        var gate = provider.HoldRefreshes(subject);
        using var browser = Cookieless(app);
        using var abort = new CancellationTokenSource();

        // Act
        var call = browser.SendAsync(ApiRequest(cookie), abort.Token);
        await gate.Arrived.Task.WaitAsync(Timeout);
        // A row lock makes the token write wait after the identity provider answered.
        await using (var rowLock = await LockRowAsync(key))
        {
            gate.Released.SetResult();
            await WaitForBlockedWriteAsync(rowLock);
            await abort.CancelAsync();
            // Give a cancellation of the write the time to reach the database. With no cancellation, it waits.
            await WaitForNoBlockedWriteAsync(rowLock, TimeSpan.FromSeconds(1));
        }

        // Assert
        await Should.ThrowAsync<OperationCanceledException>(call.WaitAsync(Timeout));

        provider.RefreshCount(subject).ShouldBe(1);
        using var timeout = new CancellationTokenSource(Timeout);
        var newRefreshToken = provider.IssuedRefreshTokens(subject)[1];
        while ((await StoredTokensAsync(app, key)).RefreshToken != newRefreshToken)
            await Task.Delay(TimeSpan.FromMilliseconds(20), timeout.Token);
        (await StoredTokensAsync(app, key)).AccessToken.ShouldBe(provider.IssuedAccessTokens(subject)[1]);
    }

    [Test]
    public async Task a_hung_identity_provider_reaches_the_token_request_timeout_and_releases_the_lock()
    {
        // Arrange
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var app = StartWithClock(clock);
        var provider = app.IdentityProvider;
        var subject = NewSubject();
        var (cookie, key) = await SignInAsync(app, $"test_sub={subject}&test_expires_in=60");
        var gate = provider.HoldRefreshes(subject);
        using var browser = Cookieless(app);
        try
        {
            // Act
            var call = browser.SendAsync(ApiRequest(cookie));
            await gate.Arrived.Task.WaitAsync(Timeout);
            clock.Advance(ClockedTokenRequestTimeout);
            var response = await call.WaitAsync(Timeout);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await response.ReadEchoAsync()).Headers["Authorization"].ShouldBe($"Bearer {provider.IssuedAccessTokens(subject).Single()}");
            provider.RefreshCount(subject).ShouldBe(1);
            (await app.WithStoreAsync(store => store.GetAsync(key, CancellationToken.None))).ShouldNotBeNull();
            (await LockCountAsync(key, granted: true)).ShouldBe(0);
        }
        finally
        {
            gate.Released.TrySetResult();
        }
    }

    [Test]
    public async Task a_wait_for_the_refresh_of_another_call_on_the_same_host_outlasts_the_lock_timeout_and_the_token_request()
    {
        // Arrange
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var app = StartWithClock(clock);
        var provider = app.IdentityProvider;
        var subject = NewSubject();
        var (cookie, key) = await SignInAsync(app, $"test_sub={subject}&test_expires_in=60");
        var gate = provider.HoldRefreshes(subject);
        using var browser = Cookieless(app);
        try
        {
            Task<HttpResponseMessage> firstCall, secondCall;
            // The first call can wait the lock timeout for the lock and then the token request timeout for the
            // identity provider. The second call still waits for its result.
            await using (await HoldLockAsync(key))
            {
                // Act
                firstCall = browser.SendAsync(ApiRequest(cookie));
                await WaitForLockWaiterAsync(key);
                secondCall = browser.SendAsync(ApiRequest(cookie));
                await WaitForInProcessWaiterAsync(app);
                clock.Advance(ClockedLockTimeout + TimeSpan.FromMilliseconds(1));
            }

            await gate.Arrived.Task.WaitAsync(Timeout);
            clock.Advance(ClockedTokenRequestTimeout - TimeSpan.FromMilliseconds(1));
            gate.Released.SetResult();
            var responses = await Task.WhenAll(firstCall, secondCall).WaitAsync(Timeout);

            // Assert
            provider.RefreshCount(subject).ShouldBe(1);
            var newAccessToken = provider.IssuedAccessTokens(subject)[1];
            foreach (var response in responses)
            {
                response.StatusCode.ShouldBe(HttpStatusCode.OK);
                (await response.ReadEchoAsync()).Headers["Authorization"].ShouldBe($"Bearer {newAccessToken}");
            }

            (await StoredTokensAsync(app, key)).AccessToken.ShouldBe(newAccessToken);
        }
        finally
        {
            gate.Released.TrySetResult();
        }
    }

    [Test]
    public async Task a_wait_for_the_refresh_of_another_call_on_the_same_host_reaches_its_limit()
    {
        // Arrange
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var app = StartWithClock(clock);
        var provider = app.IdentityProvider;
        var subject = NewSubject();
        var (cookie, key) = await SignInAsync(app, $"test_sub={subject}&test_expires_in=60");
        using var browser = Cookieless(app);
        Task<HttpResponseMessage> firstCall;
        await using (await HoldLockAsync(key))
        {
            // Act
            firstCall = browser.SendAsync(ApiRequest(cookie));
            await WaitForLockWaiterAsync(key);
            var secondCall = browser.SendAsync(ApiRequest(cookie));
            await WaitForInProcessWaiterAsync(app);
            clock.Advance(app.Services.GetRequiredService<IOptionsMonitor<RefreshOptions>>().CurrentValue.InProcessWaitLimit);

            // Assert
            // The second call stops its wait with a temporary failure and sends the current token
            var second = await secondCall.WaitAsync(Timeout);
            second.StatusCode.ShouldBe(HttpStatusCode.OK);
            var currentAccessToken = provider.IssuedAccessTokens(subject).Single();
            (await second.ReadEchoAsync()).Headers["Authorization"].ShouldBe($"Bearer {currentAccessToken}");
            firstCall.IsCompleted.ShouldBeFalse();
        }

        var first = await firstCall.WaitAsync(Timeout);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await first.ReadEchoAsync()).Headers["Authorization"].ShouldBe($"Bearer {provider.IssuedAccessTokens(subject)[1]}");
        provider.RefreshCount(subject).ShouldBe(1);
        (await app.WithStoreAsync(store => store.GetAsync(key, CancellationToken.None))).ShouldNotBeNull();
    }

    /// <summary>
    /// A BFF whose <see cref="TimeProvider"/> is <paramref name="clock"/>, so that a test moves the refresh timers
    /// with no real delay. It uses the identity provider <paramref name="identityProvider"/>, when given, and the
    /// extra <paramref name="settings"/>.
    /// </summary>
    internal static BffApp StartWithClock(FakeTimeProvider clock, FakeIdentityProvider? identityProvider = null,
        IReadOnlyDictionary<string, string?>? settings = null) => BffApp.StartAnotherInstance(
        new Dictionary<string, string?>(settings ?? new Dictionary<string, string?>())
        {
            ["Bff:Refresh:LockTimeout"] = ClockedLockTimeout.ToString(),
            ["Bff:Refresh:TokenRequestTimeout"] = ClockedTokenRequestTimeout.ToString(),
        },
        services => services.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock)),
        identityProvider);

    /// <summary>
    /// Signs in on <paramref name="app"/> and returns the session cookie and the BFF session key.
    /// </summary>
    internal static async Task<(string Cookie, string Key)> SignInAsync(BffApp app, string providerQuery)
    {
        using var browser = app.CreateBrowser();
        var callback = await BffApp.SignInAsync(browser, providerQuery: providerQuery);
        return (BffApp.SessionCookieValue(callback)!, app.SessionKeyIn(callback)!);
    }

    /// <summary>
    /// A client with no cookie container. Each request carries the cookie that the test gives it.
    /// </summary>
    internal static HttpClient Cookieless(BffApp app) =>
        app.CreateClient(new() { BaseAddress = BffApp.Origin, HandleCookies = false, AllowAutoRedirect = false });

    internal static HttpRequestMessage ApiRequest(string cookie, string path = "/api/v1/weather")
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-CSRF", "1");
        request.Headers.Add("Cookie", $"__Host-bff={cookie}");
        return request;
    }

    internal static async Task<SessionTokens> StoredTokensAsync(BffApp app, string key)
    {
        var row = (await app.WithStoreAsync(store => store.GetAsync(key, CancellationToken.None))).ShouldNotBeNull();
        return app.Services.GetRequiredService<BffTicketStore>().TokensOf(row).ShouldNotBeNull();
    }

    /// <summary>
    /// Takes the advisory lock of the BFF session <paramref name="key"/> on a connection of its own, as another
    /// BFF instance in a refresh does. Dispose the connection to release it.
    /// </summary>
    internal static async Task<NpgsqlConnection> HoldLockAsync(string key)
    {
        var connection = UnpooledConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT pg_advisory_lock(@lockId)", connection);
        command.Parameters.AddWithValue("lockId", RefreshLock.LockIdOf(key));
        await command.ExecuteNonQueryAsync();
        return connection;
    }

    /// <summary>
    /// Waits until a session waits for the advisory lock of the BFF session <paramref name="key"/>.
    /// </summary>
    private static async Task WaitForLockWaiterAsync(string key)
    {
        var lockId = RefreshLock.LockIdOf(key);
        await using var connection = new NpgsqlConnection(BffApp.Database.ConnectionString);
        await connection.OpenAsync();
        // A 64-bit advisory lock key is in classid (the high half) and objid (the low half), with objsubid 1
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM pg_locks WHERE locktype = 'advisory' AND NOT granted AND classid = @high AND objid = @low AND objsubid = 1",
            connection);
        command.Parameters.Add(new NpgsqlParameter("high", NpgsqlDbType.Oid) { Value = (uint)(lockId >> 32) });
        command.Parameters.Add(new NpgsqlParameter("low", NpgsqlDbType.Oid) { Value = (uint)lockId });

        using var timeout = new CancellationTokenSource(Timeout);
        while ((long)(await command.ExecuteScalarAsync(timeout.Token))! == 0)
            await Task.Delay(TimeSpan.FromMilliseconds(20), timeout.Token);
    }

    /// <summary>
    /// Waits until a request on <paramref name="app"/> waits for the refresh of another request in process.
    /// </summary>
    private static async Task WaitForInProcessWaiterAsync(BffApp app)
    {
        var refreshLock = (RefreshLock)app.Services.GetRequiredService<IUserTokenRequestConcurrencyControl>();
        using var timeout = new CancellationTokenSource(Timeout);
        while (refreshLock.WaitingRequests == 0)
            await Task.Delay(TimeSpan.FromMilliseconds(20), timeout.Token);
    }

    /// <summary>
    /// The number of granted, or of waiting, advisory locks of the BFF session <paramref name="key"/>.
    /// </summary>
    private static async Task<long> LockCountAsync(string key, bool granted)
    {
        var lockId = RefreshLock.LockIdOf(key);
        await using var connection = new NpgsqlConnection(BffApp.Database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM pg_locks WHERE locktype = 'advisory' AND granted = @granted AND classid = @high AND objid = @low AND objsubid = 1",
            connection);
        command.Parameters.AddWithValue("granted", granted);
        command.Parameters.Add(new NpgsqlParameter("high", NpgsqlDbType.Oid) { Value = (uint)(lockId >> 32) });
        command.Parameters.Add(new NpgsqlParameter("low", NpgsqlDbType.Oid) { Value = (uint)lockId });
        return (long)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>
    /// Locks the row of the BFF session <paramref name="key"/> in an open transaction on a connection of its own, so
    /// that a write to the row waits. Dispose the connection to release it.
    /// </summary>
    private static async Task<NpgsqlConnection> LockRowAsync(string key)
    {
        var connection = UnpooledConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("BEGIN; SELECT 1 FROM sessions WHERE key = @key FOR UPDATE", connection);
        command.Parameters.AddWithValue("key", key);
        await command.ExecuteNonQueryAsync();
        return connection;
    }

    /// <summary>
    /// Waits until a statement of another connection waits for the row lock of <paramref name="rowLock"/>.
    /// </summary>
    private static async Task WaitForBlockedWriteAsync(NpgsqlConnection rowLock)
    {
        using var timeout = new CancellationTokenSource(Timeout);
        while (await BlockedCountAsync(rowLock, timeout.Token) == 0)
            await Task.Delay(TimeSpan.FromMilliseconds(20), timeout.Token);
    }

    /// <summary>
    /// Waits until no statement waits for the row lock of <paramref name="rowLock"/>, at most <paramref name="limit"/>.
    /// </summary>
    private static async Task WaitForNoBlockedWriteAsync(NpgsqlConnection rowLock, TimeSpan limit)
    {
        var end = DateTimeOffset.UtcNow + limit;
        while (DateTimeOffset.UtcNow < end && await BlockedCountAsync(rowLock, CancellationToken.None) > 0)
            await Task.Delay(TimeSpan.FromMilliseconds(20));
    }

    private static async Task<long> BlockedCountAsync(NpgsqlConnection rowLock, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(BffApp.Database.ConnectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM pg_stat_activity WHERE @pid = ANY(pg_blocking_pids(pid))", connection);
        command.Parameters.AddWithValue("pid", rowLock.ProcessID);
        return (long)(await command.ExecuteScalarAsync(ct))!;
    }

    /// <summary>
    /// A connection outside the pool. Its dispose closes it, and Postgres then releases its locks at once. A pooled
    /// connection keeps its session, and with it a session lock, until the pool uses it again.
    /// </summary>
    private static NpgsqlConnection UnpooledConnection() =>
        new(new NpgsqlConnectionStringBuilder(BffApp.Database.ConnectionString) { Pooling = false }.ConnectionString);

    private static string NewSubject() => $"user-lock-{Guid.NewGuid():N}";
}
