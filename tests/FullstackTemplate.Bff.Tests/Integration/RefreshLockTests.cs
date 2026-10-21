namespace FullstackTemplate.Bff.Tests.Integration;

using System.Net;
using FullstackTemplate.Bff.Authentication;
using FullstackTemplate.Bff.Sessions;
using Microsoft.Extensions.DependencyInjection;
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

    [Test]
    public async Task parallel_calls_on_two_hosts_make_one_refresh_and_all_get_the_new_token()
    {
        var app = BffApp.Instance;
        var provider = app.IdentityProvider;
        var subject = NewSubject();
        await using var other = BffApp.StartAnotherInstance();
        var (cookie, key) = await SignInAsync(app, $"test_sub={subject}&test_expires_in=60&test_refresh_expires_in=3600");
        var gate = provider.HoldRefreshes(subject);

        using var first = Cookieless(app);
        using var second = Cookieless(other);
        var calls = Enumerable.Range(0, CallsPerHost)
            .SelectMany(_ => new[] { first, second })
            .Select(client => client.SendAsync(ApiRequest(cookie)))
            .ToList();

        await gate.Arrived.Task.WaitAsync(Timeout);
        // One host holds the lock and waits for the token endpoint. The other host waits for the lock.
        await WaitForLockWaiterAsync(key);
        gate.Released.SetResult();
        var responses = await Task.WhenAll(calls).WaitAsync(Timeout);

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
        await using var app = BffApp.StartAnotherInstance(new Dictionary<string, string?>
        {
            ["Bff:Refresh:LockTimeout"] = "00:00:00.300",
        });
        var provider = app.IdentityProvider;
        var subject = NewSubject();
        var (cookie, key) = await SignInAsync(app, $"test_sub={subject}&test_expires_in=60");
        await using var holder = await HoldLockAsync(key);
        using var browser = Cookieless(app);

        var response = await browser.SendAsync(ApiRequest(cookie));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadEchoAsync()).Headers["Authorization"].ShouldBe($"Bearer {provider.IssuedAccessTokens(subject).Single()}");
        provider.RefreshCount(subject).ShouldBe(0);
        (await app.WithStoreAsync(store => store.GetAsync(key, CancellationToken.None))).ShouldNotBeNull();
        BffApp.SessionCookieValue(response).ShouldBeNull();
    }

    [Test]
    public async Task an_invalid_grant_on_one_host_ends_the_session_and_the_waiter_on_the_other_host_sends_no_second_refresh()
    {
        var app = BffApp.Instance;
        var provider = app.IdentityProvider;
        var subject = NewSubject();
        await using var other = BffApp.StartAnotherInstance();
        var (cookie, key) = await SignInAsync(app, $"test_sub={subject}&test_expires_in=60&test_refresh=fail");
        var gate = provider.HoldRefreshes(subject);
        using var first = Cookieless(app);
        using var second = Cookieless(other);

        var firstCall = first.SendAsync(ApiRequest(cookie));
        await gate.Arrived.Task.WaitAsync(Timeout);
        var secondCall = second.SendAsync(ApiRequest(cookie));
        await WaitForLockWaiterAsync(key);
        gate.Released.SetResult();
        var responses = await Task.WhenAll(firstCall, secondCall).WaitAsync(Timeout);

        provider.RefreshCount(subject).ShouldBe(1);
        foreach (var response in responses)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await response.ReadEchoAsync()).Headers.ShouldNotContainKey("Authorization");
            BffApp.SessionCookieValue(response).ShouldBe("");
        }

        (await app.WithStoreAsync(store => store.GetAsync(key, CancellationToken.None))).ShouldBeNull();
    }

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
        var connection = new NpgsqlConnection(BffApp.Database.ConnectionString);
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

    private static string NewSubject() => $"user-lock-{Guid.NewGuid():N}";
}
