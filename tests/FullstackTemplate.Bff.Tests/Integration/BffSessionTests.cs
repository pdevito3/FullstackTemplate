namespace FullstackTemplate.Bff.Tests.Integration;

using System.Globalization;
using System.Net;
using FullstackTemplate.Bff.BackChannel;
using FullstackTemplate.Bff.Sessions;
using FullstackTemplate.Bff.Tests.Fakes;
using FullstackTemplate.Bff.User;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

/// <summary>
/// The BFF sessions in <c>bffdb</c>, through the endpoints of the BFF.
/// </summary>
public class BffSessionTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    [Test]
    public async Task the_cookie_holds_only_the_key_and_the_row_holds_the_tokens()
    {
        // Arrange
        var app = BffApp.Instance;
        var subject = NewSubject();
        using var browser = app.CreateBrowser();

        // Act
        var callback = await BffApp.SignInAsync(browser, providerQuery: $"test_sub={subject}");
        var cookie = BffApp.SessionCookieValue(callback);

        // Assert
        cookie.ShouldNotBeNull();
        cookie.Length.ShouldBeLessThan(600);
        callback.Headers.GetValues("Set-Cookie").ShouldNotContain(c => c.StartsWith("__Host-bffC", StringComparison.Ordinal));

        var key = app.SessionKeyIn(callback);
        key.ShouldNotBeNull();
        var row = await app.WithStoreAsync(store => store.GetAsync(key, CancellationToken.None));
        row.ShouldNotBeNull();
        row.SubjectId.ShouldBe(subject);
        row.Ticket.Length.ShouldBeGreaterThan(cookie.Length);
        System.Text.Encoding.UTF8.GetString(row.Ticket).ShouldNotContain("rt-");
    }

    [Test]
    public async Task logout_deletes_the_row()
    {
        // Arrange
        var app = BffApp.Instance;
        using var browser = app.CreateBrowser();
        var key = app.SessionKeyIn(await BffApp.SignInAsync(browser, providerQuery: $"test_sub={NewSubject()}"))!;

        // Act & Assert
        (await browser.GetAsync("/bff/logout")).StatusCode.ShouldBe(HttpStatusCode.Redirect);

        (await app.WithStoreAsync(store => store.GetAsync(key, CancellationToken.None))).ShouldBeNull();
        (await browser.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task a_new_login_gets_a_new_key_and_ends_the_old_session(bool otherSubject)
    {
        // Arrange
        var app = BffApp.Instance;
        var subject = NewSubject();
        using var browser = app.CreateBrowser();
        var oldKey = app.SessionKeyIn(await BffApp.SignInAsync(browser, providerQuery: $"test_sub={subject}"))!;

        var newSubject = otherSubject ? NewSubject() : subject;

        // Act
        var newKey = app.SessionKeyIn(await BffApp.SignInAsync(browser, providerQuery: $"test_sub={newSubject}"));

        // Assert
        newKey.ShouldNotBeNull();
        newKey.ShouldNotBe(oldKey);
        (await app.WithStoreAsync(store => store.GetAsync(oldKey, CancellationToken.None))).ShouldBeNull();
        (await app.WithStoreAsync(store => store.GetAsync(newKey, CancellationToken.None)))!.SubjectId.ShouldBe(newSubject);
        var user = await (await browser.GetApiAsync("/bff/user")).ReadJsonAsync();
        ClaimValue(user, "sub").ShouldBe(newSubject);
    }

    [Test]
    [Arguments(null, null)]
    [Arguments("sid-old", null)]
    [Arguments("sid-old", "sid-new")]
    public async Task a_new_login_revokes_the_refresh_token_of_the_old_session(string? oldSessionId, string? newSessionId)
    {
        // Arrange
        var app = BffApp.Instance;
        var provider = app.IdentityProvider;
        var subject = NewSubject();
        using var browser = app.CreateBrowser();
        await BffApp.SignInAsync(browser, providerQuery: SignInQuery(subject, oldSessionId));

        // Act
        await BffApp.SignInAsync(browser, providerQuery: SignInQuery(subject, newSessionId));

        // Assert
        var issued = provider.IssuedRefreshTokens(subject);
        issued.Count.ShouldBe(2);
        provider.RevokedTokens.ShouldContain(issued[0]);
        provider.RevokedTokens.ShouldNotContain(issued[1]);
        (await browser.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task a_new_login_in_the_same_idp_session_keeps_the_old_refresh_token()
    {
        // Arrange
        // With Keycloak, a revoke of the old refresh token would also end the client session of the new tokens
        var app = BffApp.Instance;
        var provider = app.IdentityProvider;
        var subject = NewSubject();
        using var browser = app.CreateBrowser();
        var oldKey = app.SessionKeyIn(await BffApp.SignInAsync(browser, providerQuery: SignInQuery(subject, "sid-same")))!;

        // Act
        await BffApp.SignInAsync(browser, providerQuery: SignInQuery(subject, "sid-same"));

        // Assert
        provider.IssuedRefreshTokens(subject).Count.ShouldBe(2);
        provider.WasRefreshTokenRevoked(subject).ShouldBeFalse();
        (await app.WithStoreAsync(store => store.GetAsync(oldKey, CancellationToken.None))).ShouldBeNull();
        (await browser.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task a_refresh_that_ends_after_logout_does_not_bring_the_session_back_and_revokes_its_refresh_token()
    {
        // Arrange
        var app = BffApp.Instance;
        var provider = app.IdentityProvider;
        var subject = NewSubject();
        using var browser = app.CreateBrowser();
        var key = app.SessionKeyIn(await BffApp.SignInAsync(browser, providerQuery: $"test_sub={subject}&test_expires_in=60"))!;
        var gate = provider.HoldRefreshes(subject);

        var apiCall = browser.GetApiAsync("/api/v1/items");
        await gate.Arrived.Task.WaitAsync(Timeout);

        // Act & Assert
        (await browser.GetAsync("/bff/logout")).StatusCode.ShouldBe(HttpStatusCode.Redirect);
        gate.Released.SetResult();
        (await apiCall.WaitAsync(Timeout)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await app.WithStoreAsync(store => store.GetAsync(key, CancellationToken.None))).ShouldBeNull();
        (await app.WithStoreAsync(store => store.FindBySubjectAsync(subject, CancellationToken.None))).ShouldBeEmpty();
        var issued = provider.IssuedRefreshTokens(subject);
        issued.Count.ShouldBe(2);
        provider.RevokedTokens.ShouldContain(issued[0]);
        provider.RevokedTokens.ShouldContain(issued[1]);
        (await browser.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task a_refresh_that_writes_between_the_read_and_the_delete_of_a_logout_gets_its_refresh_token_revoked()
    {
        // Arrange
        var deletes = new DeleteGate();
        await using var app = BffApp.StartAnotherInstance(services: services => services.AddScoped<IBffSessionStore>(
            sp => new HeldDeleteStore(ActivatorUtilities.CreateInstance<EfBffSessionStore>(sp), deletes)));
        var provider = app.IdentityProvider;
        var subject = NewSubject();
        using var browser = app.CreateBrowser();
        var key = app.SessionKeyIn(await BffApp.SignInAsync(browser, providerQuery: $"test_sub={subject}&test_expires_in=60"))!;
        var refreshes = provider.HoldRefreshes(subject);

        // The API call reads the row, and then its refresh waits at the identity provider
        var apiCall = browser.GetApiAsync("/api/v1/items");
        await refreshes.Arrived.Task.WaitAsync(Timeout);
        // The logout reads the row with the first refresh token, and then its delete waits
        var logout = browser.GetAsync("/bff/logout");
        await deletes.Arrived.Task.WaitAsync(Timeout);

        // Act
        // The refresh writes the second refresh token to the row before the delete
        refreshes.Released.SetResult();

        // Assert
        (await apiCall.WaitAsync(Timeout)).StatusCode.ShouldBe(HttpStatusCode.OK);
        deletes.Released.SetResult();
        (await logout.WaitAsync(Timeout)).StatusCode.ShouldBe(HttpStatusCode.Redirect);

        (await app.WithStoreAsync(store => store.GetAsync(key, CancellationToken.None))).ShouldBeNull();
        var issued = provider.IssuedRefreshTokens(subject);
        issued.Count.ShouldBe(2);
        provider.RevokedTokens.ShouldContain(issued[1]);
        (await browser.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task a_token_write_that_races_a_sliding_renewal_keeps_the_later_expiry()
    {
        // Arrange
        // Two BFF instances on one bffdb. The clock of the second is 6 minutes ahead, so a request there is past
        // half of the sliding expiry. The same race occurs with any clock skew, or any delay between the time that
        // a write takes and the write itself.
        var behind = new TestClock(DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        var ahead = new TestClock(behind.Now.AddMinutes(6));
        var settings = new Dictionary<string, string?> { ["Bff:Session:SlidingExpiration"] = "00:10:00" };
        await using var refreshing = BffApp.StartAnotherInstance(settings, services => services.AddSingleton<TimeProvider>(behind));
        await using var sliding = BffApp.StartAnotherInstance(settings, services => services.AddSingleton<TimeProvider>(ahead));
        var cookies = new CookieContainer();
        using var refreshingBrowser = refreshing.CreateBrowser(cookies);
        using var slidingBrowser = sliding.CreateBrowser(cookies);
        var provider = refreshing.IdentityProvider;
        var subject = NewSubject();
        var key = refreshing.SessionKeyIn(await BffApp.SignInAsync(refreshingBrowser, providerQuery: $"test_sub={subject}&test_expires_in=60"))!;
        var gate = provider.HoldRefreshes(subject);

        // The API call reads the row, and then its refresh waits at the identity provider
        var apiCall = refreshingBrowser.GetApiAsync("/api/v1/items");
        await gate.Arrived.Task.WaitAsync(Timeout);

        // Act & Assert
        // The sliding renewal writes the row while the refresh waits
        (await slidingBrowser.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.OK);
        var slid = (await refreshing.WithStoreAsync(store => store.GetAsync(key, CancellationToken.None)))!;
        slid.Expires.ShouldBe(ahead.Now.AddMinutes(10));
        gate.Released.SetResult();
        (await apiCall.WaitAsync(Timeout)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var row = (await refreshing.WithStoreAsync(store => store.GetAsync(key, CancellationToken.None)))!;
        row.Ticket.ShouldNotBe(slid.Ticket, "the token write did not land");
        row.Expires.ShouldBe(slid.Expires);
        row.Renewed.ShouldBe(slid.Renewed);
    }

    [Test]
    public async Task a_new_login_deletes_the_chunks_of_the_old_cookie()
    {
        // Arrange
        // A BFF with a small chunk size gives the first login a cookie in many chunks
        await using var chunking = BffApp.StartAnotherInstance(services: services =>
            services.PostConfigure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme,
                cookie => cookie.CookieManager = new ChunkingCookieManager { ChunkSize = 200 }));
        var app = BffApp.Instance;
        var cookies = new CookieContainer();
        using var chunkingBrowser = chunking.CreateBrowser(cookies);
        using var browser = app.CreateBrowser(cookies);
        var oldLogin = await BffApp.SignInAsync(chunkingBrowser, providerQuery: $"test_sub={NewSubject()}");
        var chunks = ChunkCookieNames(cookies);
        chunks.Count.ShouldBeGreaterThan(2);
        BffApp.SessionCookieValue(oldLogin).ShouldStartWith("chunks-");

        // Act
        var newLogin = await BffApp.SignInAsync(browser, providerQuery: $"test_sub={NewSubject()}");

        // Assert
        var setCookies = newLogin.Headers.GetValues("Set-Cookie").ToList();
        // A browser takes a __Host- cookie, also a delete, only with Secure and Path=/
        foreach (var chunk in chunks)
            setCookies.ShouldContain(c => c.StartsWith($"{chunk}=;", StringComparison.Ordinal)
                    && c.Contains("expires=Thu, 01 Jan 1970") && c.Contains("; secure") && c.Contains("; path=/;"),
                $"the callback does not delete {chunk}");
        ChunkCookieNames(cookies).ShouldBeEmpty();
        BffApp.SessionCookieValue(newLogin).ShouldNotStartWith("chunks-");
        (await browser.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task a_refresh_with_no_id_token_keeps_the_id_token_for_logout()
    {
        // Arrange
        var app = BffApp.Instance;
        var provider = app.IdentityProvider;
        var subject = NewSubject();
        using var browser = app.CreateBrowser();
        await BffApp.SignInAsync(browser, providerQuery: $"test_sub={subject}&test_expires_in=60");
        (await browser.GetApiAsync("/api/v1/items")).StatusCode.ShouldBe(HttpStatusCode.OK);
        provider.RefreshCount(subject).ShouldBe(1);

        // Act
        var logout = await browser.GetAsync("/bff/logout");

        // Assert
        var idTokens = provider.IssuedIdTokens(subject);
        idTokens.Count.ShouldBe(1);
        BffApp.Query(logout.Headers.Location!)["id_token_hint"].ShouldBe(idTokens[0]);
    }

    [Test]
    public async Task the_absolute_limit_caps_the_session_expiry()
    {
        // Arrange
        await using var app = BffApp.StartAnotherInstance(new Dictionary<string, string?>
        {
            ["Bff:Session:AbsoluteLifetime"] = "01:00:00",
        });
        using var browser = app.CreateBrowser();
        var key = app.SessionKeyIn(await BffApp.SignInAsync(browser, providerQuery: $"test_sub={NewSubject()}"))!;

        // Act
        var user = await (await browser.GetApiAsync("/bff/user")).ReadJsonAsync();

        // Assert
        double.Parse(ClaimValue(user, UserEndpoint.SessionExpiresInClaim), CultureInfo.InvariantCulture)
            .ShouldBeInRange(3500, 3600);
        var row = (await app.WithStoreAsync(store => store.GetAsync(key, CancellationToken.None)))!;
        row.AbsoluteExpires.ShouldBe(row.Created.AddHours(1));
        row.Expires.ShouldBeLessThanOrEqualTo(row.AbsoluteExpires);
    }

    [Test]
    public async Task the_session_expires_in_of_bff_user_comes_from_the_clock_of_the_bff()
    {
        // Arrange
        var clock = new TestClock(DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        await using var app = BffApp.StartAnotherInstance(services: services => services.AddSingleton<TimeProvider>(clock));
        using var browser = app.CreateBrowser();
        await BffApp.SignInAsync(browser, providerQuery: $"test_sub={NewSubject()}");
        clock.Advance(TimeSpan.FromHours(2));

        // Act
        var user = await (await browser.GetApiAsync("/bff/user")).ReadJsonAsync();

        // Assert
        double.Parse(ClaimValue(user, UserEndpoint.SessionExpiresInClaim), CultureInfo.InvariantCulture)
            .ShouldBe((TimeSpan.FromDays(14) - TimeSpan.FromHours(2)).TotalSeconds);
    }

    [Test]
    public async Task bff_user_with_slide_false_keeps_the_row_expiry_and_a_plain_call_slides_it()
    {
        // Arrange
        var clock = new TestClock(DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        await using var app = BffApp.StartAnotherInstance(
            new Dictionary<string, string?> { ["Bff:Session:SlidingExpiration"] = "00:10:00" },
            services => services.AddSingleton<TimeProvider>(clock));
        using var browser = app.CreateBrowser();
        var key = app.SessionKeyIn(await BffApp.SignInAsync(browser, providerQuery: $"test_sub={NewSubject()}"))!;
        var signedIn = clock.Now;
        // Past half of the sliding expiry, so that a call renews it
        clock.Advance(TimeSpan.FromMinutes(6));

        // Act & Assert
        (await browser.GetApiAsync("/bff/user?slide=false")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await app.WithStoreAsync(store => store.GetAsync(key, CancellationToken.None)))!.Expires
            .ShouldBe(signedIn.AddMinutes(10));

        (await browser.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await app.WithStoreAsync(store => store.GetAsync(key, CancellationToken.None)))!.Expires
            .ShouldBe(clock.Now.AddMinutes(10));
    }

    [Test]
    public async Task another_bff_instance_reads_the_session_with_the_shared_data_protection_keys()
    {
        // Arrange
        using var browser = BffApp.Instance.CreateBrowser();
        var subject = NewSubject();
        var cookie = BffApp.SessionCookieValue(await BffApp.SignInAsync(browser, providerQuery: $"test_sub={subject}"))!;
        await using var other = BffApp.StartAnotherInstance();
        using var otherBrowser = other.CreateClient(new() { BaseAddress = BffApp.Origin, HandleCookies = false });

        var request = new HttpRequestMessage(HttpMethod.Get, "/bff/user");
        request.Headers.Add("X-CSRF", "1");
        request.Headers.Add("Cookie", $"__Host-bff={cookie}");

        // Act
        var response = await otherBrowser.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ClaimValue(await response.ReadJsonAsync(), "sub").ShouldBe(subject);
        await using var scope = other.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<BffDbContext>().DataProtectionKeys.CountAsync()).ShouldBeGreaterThan(0);
    }

    [Test]
    [NotInParallel(BffSessionStoreContractTests.EndedRowDeletes)]
    public async Task the_cleanup_deletes_the_ended_sessions_and_keeps_the_live_ones()
    {
        // Arrange
        var app = BffApp.Instance;
        var past = new TestClock(DateTimeOffset.UtcNow.AddDays(-2));
        var slidingEnded = BffSessionStoreContractTests.NewSession(past, TimeSpan.FromHours(1), TimeSpan.FromDays(30));
        var absoluteEnded = BffSessionStoreContractTests.NewSession(past, TimeSpan.FromDays(14), TimeSpan.FromDays(1));
        var live = BffSessionStoreContractTests.NewSession(TimeProvider.System);
        foreach (var session in new[] { slidingEnded, absoluteEnded, live })
            await app.WithStoreAsync(store => store.CreateAsync(session, CancellationToken.None));

        // Act
        var deleted = await app.Services.GetRequiredService<BffSessionCleanupService>().CleanupAsync(CancellationToken.None);

        // Assert
        deleted.ShouldBeGreaterThanOrEqualTo(2);
        (await app.WithStoreAsync(store => store.DeleteAsync(slidingEnded.Key, CancellationToken.None))).ShouldBeNull();
        (await app.WithStoreAsync(store => store.DeleteAsync(absoluteEnded.Key, CancellationToken.None))).ShouldBeNull();
        (await app.WithStoreAsync(store => store.GetAsync(live.Key, CancellationToken.None))).ShouldNotBeNull();
    }

    [Test]
    [NotInParallel(BffSessionStoreContractTests.EndedRowDeletes)]
    public async Task the_cleanup_deletes_the_expired_logout_tokens_and_keeps_the_others()
    {
        // Arrange
        var app = BffApp.Instance;
        var expiredJti = $"jti-{Guid.NewGuid():N}";
        var liveJti = $"jti-{Guid.NewGuid():N}";
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var replayCache = scope.ServiceProvider.GetRequiredService<LogoutTokenReplayCache>();
            (await replayCache.TryAddAsync(expiredJti, DateTimeOffset.UtcNow.AddMinutes(-1), CancellationToken.None)).ShouldBeTrue();
            (await replayCache.TryAddAsync(liveJti, DateTimeOffset.UtcNow.AddMinutes(10), CancellationToken.None)).ShouldBeTrue();
            (await replayCache.TryAddAsync(liveJti, DateTimeOffset.UtcNow.AddMinutes(10), CancellationToken.None)).ShouldBeFalse();
        }

        // Act
        await app.Services.GetRequiredService<BffSessionCleanupService>().CleanupAsync(CancellationToken.None);

        // Assert
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var replayCache = scope.ServiceProvider.GetRequiredService<LogoutTokenReplayCache>();
            (await replayCache.IsUsedAsync(expiredJti, CancellationToken.None)).ShouldBeFalse();
            (await replayCache.IsUsedAsync(liveJti, CancellationToken.None)).ShouldBeTrue();
        }
    }

    /// <summary>
    /// See <see cref="HeldDeleteStore"/>.
    /// </summary>
    private sealed class DeleteGate
    {
        public TaskCompletionSource Arrived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Released { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// The BFF session store, with each delete held until the test releases the gate.
    /// </summary>
    private sealed class HeldDeleteStore(IBffSessionStore inner, DeleteGate gate) : IBffSessionStore
    {
        public Task<BffSession> CreateAsync(BffSession session, CancellationToken cancellationToken) =>
            inner.CreateAsync(session, cancellationToken);

        public Task<BffSession?> GetAsync(string key, CancellationToken cancellationToken) => inner.GetAsync(key, cancellationToken);

        public Task<BffSessionWrite> RenewAsync(string key, byte[] ticket, DateTimeOffset renewed, DateTimeOffset expires,
            uint expectedVersion, CancellationToken cancellationToken) =>
            inner.RenewAsync(key, ticket, renewed, expires, expectedVersion, cancellationToken);

        public Task<BffSessionWrite> RenewExpiryAsync(string key, DateTimeOffset renewed, DateTimeOffset expires,
            uint expectedVersion, CancellationToken cancellationToken) =>
            inner.RenewExpiryAsync(key, renewed, expires, expectedVersion, cancellationToken);

        public async Task<BffSession?> DeleteAsync(string key, CancellationToken cancellationToken)
        {
            gate.Arrived.TrySetResult();
            await gate.Released.Task;
            return await inner.DeleteAsync(key, cancellationToken);
        }

        public Task<IReadOnlyList<BffSession>> FindBySubjectAsync(string subjectId, CancellationToken cancellationToken) =>
            inner.FindBySubjectAsync(subjectId, cancellationToken);

        public Task<IReadOnlyList<BffSession>> FindBySessionIdAsync(string sessionId, CancellationToken cancellationToken) =>
            inner.FindBySessionIdAsync(sessionId, cancellationToken);

        public Task<int> DeleteEndedAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken) =>
            inner.DeleteEndedAsync(now, batchSize, cancellationToken);
    }

    private static List<string> ChunkCookieNames(CookieContainer cookies) =>
        cookies.GetAllCookies().Where(c => c.Name.StartsWith("__Host-bffC", StringComparison.Ordinal)).Select(c => c.Name).ToList();

    private static string NewSubject() => $"user-session-{Guid.NewGuid():N}";

    private static string SignInQuery(string subject, string? sessionId) =>
        sessionId is null ? $"test_sub={subject}" : $"test_sub={subject}&test_sid={sessionId}";

    private static string ClaimValue(System.Text.Json.JsonElement claims, string type) =>
        claims.EnumerateArray()
            .Where(c => c.GetProperty("type").GetString() == type)
            .Select(c => c.GetProperty("value").ToString())
            .Single();
}
