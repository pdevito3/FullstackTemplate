namespace FullstackTemplate.Bff.Tests.Integration;

using System.Net;
using FullstackTemplate.Bff.Sessions;
using FullstackTemplate.Bff.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

/// <summary>
/// The counters of section 3.9 of the spec: <c>bff.sessions.started</c>, <c>bff.sessions.ended</c>, and
/// <c>bff.refresh.failures</c>.
/// </summary>
// The listener sees the measurements of the whole process, so no other test may run at the same time
[NotInParallel]
public sealed class BffMetricsTests : IDisposable
{
    private readonly MetricRecorder _metrics = new();

    public void Dispose() => _metrics.Dispose();

    [Test]
    public async Task a_login_starts_one_session_and_a_logout_ends_it_with_reason_logout()
    {
        using var browser = BffApp.Instance.CreateBrowser();

        await BffApp.SignInAsync(browser, providerQuery: $"test_sub={NewSubject()}");
        (await browser.GetAsync("/bff/logout")).StatusCode.ShouldBe(HttpStatusCode.Redirect);

        _metrics.Sum("bff.sessions.started").ShouldBe(1);
        _metrics.TagsOf("bff.sessions.started").ShouldAllBe(tags => tags.Count == 0);
        _metrics.Sum("bff.sessions.ended").ShouldBe(1);
        _metrics.Sum("bff.sessions.ended", ("reason", "logout")).ShouldBe(1);
    }

    [Test]
    public async Task a_back_channel_logout_ends_each_session_with_reason_backchannel_logout()
    {
        var subject = NewSubject();
        using var first = BffApp.Instance.CreateBrowser();
        using var second = BffApp.Instance.CreateBrowser();
        await BffApp.SignInAsync(first, providerQuery: $"test_sub={subject}");
        await BffApp.SignInAsync(second, providerQuery: $"test_sub={subject}");

        var response = await BackChannelLogoutTests.PostAsync(BackChannelLogoutTests.Token(c => { c["sub"] = subject; c.Remove("sid"); }));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _metrics.Sum("bff.sessions.ended").ShouldBe(2);
        _metrics.Sum("bff.sessions.ended", ("reason", "backchannel_logout")).ShouldBe(2);
    }

    [Test]
    public async Task the_cleanup_ends_each_deleted_session_with_reason_expiry()
    {
        var app = BffApp.Instance;
        var past = new TestClock(DateTimeOffset.UtcNow.AddDays(-2));
        foreach (var session in new[]
                 {
                     BffSessionStoreContractTests.NewSession(past, TimeSpan.FromHours(1), TimeSpan.FromDays(30)),
                     BffSessionStoreContractTests.NewSession(past, TimeSpan.FromDays(14), TimeSpan.FromDays(1)),
                 })
            await app.WithStoreAsync(store => store.CreateAsync(session, CancellationToken.None));

        var deleted = await app.Services.GetRequiredService<BffSessionCleanupService>().CleanupAsync(CancellationToken.None);

        deleted.ShouldBeGreaterThanOrEqualTo(2);
        _metrics.Sum("bff.sessions.ended").ShouldBe(deleted);
        _metrics.Sum("bff.sessions.ended", ("reason", "expiry")).ShouldBe(deleted);
    }

    [Test]
    public async Task a_refresh_with_invalid_grant_is_one_permanent_failure()
    {
        using var browser = BffApp.Instance.CreateBrowser();
        await BffApp.SignInAsync(browser, providerQuery: $"test_sub={NewSubject()}&test_expires_in=60&test_refresh=fail");

        (await browser.GetApiAsync("/api/v1/items")).StatusCode.ShouldBe(HttpStatusCode.OK);

        _metrics.Sum("bff.refresh.failures").ShouldBe(1);
        _metrics.Sum("bff.refresh.failures", ("kind", "permanent")).ShouldBe(1);
        _metrics.Sum("bff.sessions.ended").ShouldBe(1);
        _metrics.Sum("bff.sessions.ended", ("reason", "refresh_failure")).ShouldBe(1);
    }

    [Test]
    public async Task a_refresh_with_a_503_is_one_temporary_failure_and_ends_no_session()
    {
        using var browser = BffApp.Instance.CreateBrowser();
        await BffApp.SignInAsync(browser, providerQuery: $"test_sub={NewSubject()}&test_expires_in=60&test_refresh=unavailable");

        (await browser.GetApiAsync("/api/v1/items")).StatusCode.ShouldBe(HttpStatusCode.OK);

        _metrics.Sum("bff.refresh.failures").ShouldBe(1);
        _metrics.Sum("bff.refresh.failures", ("kind", "temporary")).ShouldBe(1);
        _metrics.Sum("bff.sessions.ended").ShouldBe(0);
    }

    [Test]
    public async Task a_lock_timeout_is_one_temporary_failure_and_ends_no_session()
    {
        await using var app = BffApp.StartAnotherInstance(new Dictionary<string, string?>
        {
            ["Bff:Refresh:LockTimeout"] = "00:00:00.300",
            ["Bff:Refresh:TokenRequestTimeout"] = "00:00:00.200",
        });
        var (cookie, key) = await RefreshLockTests.SignInAsync(app, $"test_sub={NewSubject()}&test_expires_in=60");
        await using var holder = await RefreshLockTests.HoldLockAsync(key);
        using var browser = RefreshLockTests.Cookieless(app);

        (await browser.SendAsync(RefreshLockTests.ApiRequest(cookie))).StatusCode.ShouldBe(HttpStatusCode.OK);

        _metrics.Sum("bff.refresh.failures").ShouldBe(1);
        _metrics.Sum("bff.refresh.failures", ("kind", "temporary")).ShouldBe(1);
        _metrics.Sum("bff.sessions.ended").ShouldBe(0);
    }

    [Test]
    public async Task a_successful_refresh_is_no_failure()
    {
        using var browser = BffApp.Instance.CreateBrowser();
        await BffApp.SignInAsync(browser, providerQuery: $"test_sub={NewSubject()}&test_expires_in=60");

        (await browser.GetApiAsync("/api/v1/items")).StatusCode.ShouldBe(HttpStatusCode.OK);

        _metrics.Sum("bff.refresh.failures").ShouldBe(0);
    }

    private static string NewSubject() => $"user-metrics-{Guid.NewGuid():N}";
}
