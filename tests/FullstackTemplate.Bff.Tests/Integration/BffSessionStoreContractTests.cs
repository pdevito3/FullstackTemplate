namespace FullstackTemplate.Bff.Tests.Integration;

using FullstackTemplate.Bff.Sessions;
using FullstackTemplate.Bff.Tests.Fakes;
using Microsoft.EntityFrameworkCore;
using Shouldly;

/// <summary>
/// The rules of the <see cref="IBffSessionStore"/> seam. Each test runs on both adapters: the EF Core adapter on
/// the <c>bffdb</c> of the test run, and the in-memory adapter.
/// </summary>
public class BffSessionStoreContractTests
{
    public const string Memory = "memory";
    public const string Postgres = "postgres";

    /// <summary>
    /// The tests that delete ended rows run one at a time, because a delete removes each ended row in the
    /// database, also the rows of other tests.
    /// </summary>
    public const string EndedRowDeletes = "ended-row-deletes";

    [Test]
    [Arguments(Memory)]
    [Arguments(Postgres)]
    public async Task a_created_row_reads_back_with_a_version(string adapter)
    {
        var clock = NowClock();
        await using var store = await StoreUnderTest.OpenAsync(adapter, clock);
        var session = NewSession(clock, sessionId: "sid-read");

        var created = await store.Store.CreateAsync(session, CancellationToken.None);
        var read = await store.Store.GetAsync(session.Key, CancellationToken.None);

        read.ShouldNotBeNull();
        read.Version.ShouldBe(created.Version);
        read.SubjectId.ShouldBe(session.SubjectId);
        read.SessionId.ShouldBe("sid-read");
        read.Created.ShouldBe(session.Created);
        read.Expires.ShouldBe(session.Expires);
        read.AbsoluteExpires.ShouldBe(session.AbsoluteExpires);
        read.Ticket.ShouldBe(session.Ticket);
    }

    [Test]
    [Arguments(Memory, 1, 5)]
    [Arguments(Memory, 5, 1)]
    [Arguments(Postgres, 1, 5)]
    [Arguments(Postgres, 5, 1)]
    public async Task a_read_rejects_a_row_past_its_sliding_expiry_or_its_absolute_limit(string adapter, int slidingHours, int absoluteHours)
    {
        var clock = NowClock();
        await using var store = await StoreUnderTest.OpenAsync(adapter, clock);
        var session = NewSession(clock, TimeSpan.FromHours(slidingHours), TimeSpan.FromHours(absoluteHours));
        await store.Store.CreateAsync(session, CancellationToken.None);

        clock.Advance(TimeSpan.FromHours(Math.Min(slidingHours, absoluteHours)) - TimeSpan.FromSeconds(1));
        (await store.Store.GetAsync(session.Key, CancellationToken.None)).ShouldNotBeNull();

        clock.Advance(TimeSpan.FromSeconds(1));
        (await store.Store.GetAsync(session.Key, CancellationToken.None)).ShouldBeNull();
        (await store.Store.FindBySubjectAsync(session.SubjectId, CancellationToken.None)).ShouldBeEmpty();
    }

    [Test]
    [Arguments(Memory)]
    [Arguments(Postgres)]
    public async Task a_renewal_with_the_read_version_writes_and_moves_the_version(string adapter)
    {
        var clock = NowClock();
        await using var store = await StoreUnderTest.OpenAsync(adapter, clock);
        var created = await store.Store.CreateAsync(NewSession(clock), CancellationToken.None);

        var renewed = clock.Now.AddMinutes(1);
        var write = await store.Store.RenewAsync(created.Key, [9, 9], renewed, renewed.AddHours(2), created.Version, CancellationToken.None);

        var written = write.ShouldBeOfType<BffSessionWrite.Written>();
        written.Version.ShouldNotBe(created.Version);
        var read = (await store.Store.GetAsync(created.Key, CancellationToken.None))!;
        read.Version.ShouldBe(written.Version);
        read.Ticket.ShouldBe(new byte[] { 9, 9 });
        read.Renewed.ShouldBe(renewed);
        read.Expires.ShouldBe(renewed.AddHours(2));
        read.Created.ShouldBe(created.Created);
        read.AbsoluteExpires.ShouldBe(created.AbsoluteExpires);
    }

    [Test]
    [Arguments(Memory)]
    [Arguments(Postgres)]
    public async Task a_write_with_an_old_version_changes_nothing_and_returns_the_current_row(string adapter)
    {
        var clock = NowClock();
        await using var store = await StoreUnderTest.OpenAsync(adapter, clock);
        var created = await store.Store.CreateAsync(NewSession(clock), CancellationToken.None);
        var first = (BffSessionWrite.Written)await store.Store.RenewAsync(
            created.Key, [1], clock.Now, clock.Now.AddHours(1), created.Version, CancellationToken.None);

        var late = await store.Store.RenewAsync(created.Key, [2], clock.Now, clock.Now.AddHours(3), created.Version, CancellationToken.None);
        var lateExpiry = await store.Store.RenewExpiryAsync(created.Key, clock.Now, clock.Now.AddHours(3), created.Version, CancellationToken.None);

        late.ShouldBeOfType<BffSessionWrite.Conflict>().Current.Version.ShouldBe(first.Version);
        lateExpiry.ShouldBeOfType<BffSessionWrite.Conflict>().Current.Ticket.ShouldBe(new byte[] { 1 });
        var read = (await store.Store.GetAsync(created.Key, CancellationToken.None))!;
        read.Ticket.ShouldBe(new byte[] { 1 });
        read.Expires.ShouldBe(clock.Now.AddHours(1));
    }

    [Test]
    [Arguments(Memory)]
    [Arguments(Postgres)]
    public async Task an_expiry_renewal_keeps_the_ticket(string adapter)
    {
        var clock = NowClock();
        await using var store = await StoreUnderTest.OpenAsync(adapter, clock);
        var created = await store.Store.CreateAsync(NewSession(clock), CancellationToken.None);

        var write = await store.Store.RenewExpiryAsync(created.Key, clock.Now, clock.Now.AddHours(5), created.Version, CancellationToken.None);

        write.ShouldBeOfType<BffSessionWrite.Written>();
        var read = (await store.Store.GetAsync(created.Key, CancellationToken.None))!;
        read.Ticket.ShouldBe(created.Ticket);
        read.Expires.ShouldBe(clock.Now.AddHours(5));
    }

    [Test]
    [Arguments(Memory)]
    [Arguments(Postgres)]
    public async Task a_write_to_a_deleted_row_is_missing_and_creates_no_row(string adapter)
    {
        var clock = NowClock();
        await using var store = await StoreUnderTest.OpenAsync(adapter, clock);
        var created = await store.Store.CreateAsync(NewSession(clock), CancellationToken.None);

        (await store.Store.DeleteAsync(created.Key, CancellationToken.None))!.Key.ShouldBe(created.Key);
        var write = await store.Store.RenewAsync(created.Key, [3], clock.Now, clock.Now.AddHours(1), created.Version, CancellationToken.None);
        var expiryWrite = await store.Store.RenewExpiryAsync(created.Key, clock.Now, clock.Now.AddHours(1), created.Version, CancellationToken.None);

        write.ShouldBeOfType<BffSessionWrite.Missing>();
        expiryWrite.ShouldBeOfType<BffSessionWrite.Missing>();
        (await store.Store.GetAsync(created.Key, CancellationToken.None)).ShouldBeNull();
        (await store.Store.DeleteAsync(created.Key, CancellationToken.None)).ShouldBeNull();
    }

    [Test]
    [Arguments(Memory)]
    [Arguments(Postgres)]
    public async Task the_delete_returns_the_row_with_the_last_write(string adapter)
    {
        var clock = NowClock();
        await using var store = await StoreUnderTest.OpenAsync(adapter, clock);
        var created = await store.Store.CreateAsync(NewSession(clock), CancellationToken.None);
        var written = (BffSessionWrite.Written)await store.Store.RenewAsync(created.Key, [9], clock.Now, clock.Now.AddHours(1),
            created.Version, CancellationToken.None);

        var deleted = await store.Store.DeleteAsync(created.Key, CancellationToken.None);

        deleted.ShouldNotBeNull();
        deleted.Key.ShouldBe(created.Key);
        deleted.SubjectId.ShouldBe(created.SubjectId);
        deleted.Ticket.ShouldBe([9]);
        deleted.Expires.ShouldBe(clock.Now.AddHours(1));
        deleted.Version.ShouldBe(written.Version);
    }

    [Test]
    [Arguments(Memory)]
    [Arguments(Postgres)]
    public async Task the_rows_are_found_by_sub_and_by_sid(string adapter)
    {
        var clock = NowClock();
        await using var store = await StoreUnderTest.OpenAsync(adapter, clock);
        var subject = $"sub-{Guid.NewGuid():N}";
        var sessionId = $"sid-{Guid.NewGuid():N}";
        var first = await store.Store.CreateAsync(NewSession(clock, subjectId: subject, sessionId: sessionId), CancellationToken.None);
        var second = await store.Store.CreateAsync(NewSession(clock, subjectId: subject), CancellationToken.None);
        await store.Store.CreateAsync(NewSession(clock), CancellationToken.None);

        (await store.Store.FindBySubjectAsync(subject, CancellationToken.None)).Select(s => s.Key)
            .ShouldBe([first.Key, second.Key], ignoreOrder: true);
        (await store.Store.FindBySessionIdAsync(sessionId, CancellationToken.None)).Select(s => s.Key)
            .ShouldBe([first.Key]);
    }

    [Test]
    [NotInParallel(EndedRowDeletes)]
    [Arguments(Memory)]
    [Arguments(Postgres)]
    public async Task the_ended_rows_are_deleted_in_batches_and_the_live_rows_stay(string adapter)
    {
        // A clock in the past, so that no row of another test has ended at the time of the delete
        var clock = new TestClock(new DateTimeOffset(1990, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await using var store = await StoreUnderTest.OpenAsync(adapter, clock);
        var ended = new List<string>();
        for (var i = 0; i < 5; i++)
            ended.Add((await store.Store.CreateAsync(NewSession(clock, TimeSpan.FromMinutes(1), TimeSpan.FromDays(1)), CancellationToken.None)).Key);
        ended.Add((await store.Store.CreateAsync(NewSession(clock, TimeSpan.FromDays(1), TimeSpan.FromMinutes(1)), CancellationToken.None)).Key);
        var live = await store.Store.CreateAsync(NewSession(clock, TimeSpan.FromDays(1), TimeSpan.FromDays(1)), CancellationToken.None);

        var now = clock.Now.AddMinutes(2);
        var first = await store.Store.DeleteEndedAsync(now, 4, CancellationToken.None);
        var second = await store.Store.DeleteEndedAsync(now, 4, CancellationToken.None);
        var third = await store.Store.DeleteEndedAsync(now, 4, CancellationToken.None);

        first.ShouldBe(4);
        second.ShouldBe(2);
        third.ShouldBe(0);
        foreach (var key in ended)
            (await store.Store.DeleteAsync(key, CancellationToken.None)).ShouldBeNull();
        (await store.Store.GetAsync(live.Key, CancellationToken.None)).ShouldNotBeNull();
    }

    /// <summary>
    /// A clock at the current time, in whole seconds, so that the rows of these tests have not ended for the
    /// cleanup of the integration tests.
    /// </summary>
    private static TestClock NowClock()
    {
        var now = DateTimeOffset.UtcNow;
        return new TestClock(new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, TimeSpan.Zero));
    }

    public static BffSession NewSession(TimeProvider clock, TimeSpan? sliding = null, TimeSpan? absolute = null,
        string? subjectId = null, string? sessionId = null)
    {
        var now = clock.GetUtcNow();
        return new BffSession(
            Key: SessionKey.Create(),
            SubjectId: subjectId ?? $"sub-{Guid.NewGuid():N}",
            SessionId: sessionId,
            Created: now,
            Renewed: now,
            Expires: now + (sliding ?? TimeSpan.FromHours(1)),
            AbsoluteExpires: now + (absolute ?? TimeSpan.FromHours(2)),
            Ticket: [1, 2, 3],
            Version: 0);
    }

    /// <summary>
    /// One adapter for one test, with the resources that it needs.
    /// </summary>
    public sealed class StoreUnderTest : IAsyncDisposable
    {
        private readonly BffDbContext? _dbContext;

        private StoreUnderTest(IBffSessionStore store, BffDbContext? dbContext)
        {
            Store = store;
            _dbContext = dbContext;
        }

        public IBffSessionStore Store { get; }

        public static Task<StoreUnderTest> OpenAsync(string adapter, TimeProvider clock)
        {
            if (adapter == Memory)
                return Task.FromResult(new StoreUnderTest(new InMemoryBffSessionStore(clock), null));

            var options = new DbContextOptionsBuilder<BffDbContext>();
            BffSessionExtensions.ConfigureBffDb(options, BffApp.Database.ConnectionString);
            var dbContext = new BffDbContext(options.Options);
            return Task.FromResult(new StoreUnderTest(new EfBffSessionStore(dbContext, clock), dbContext));
        }

        public ValueTask DisposeAsync() => _dbContext?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}
