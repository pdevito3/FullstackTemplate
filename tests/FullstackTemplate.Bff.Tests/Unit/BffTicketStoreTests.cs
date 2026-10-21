namespace FullstackTemplate.Bff.Tests.Unit;

using System.Security.Claims;
using FullstackTemplate.Bff.Sessions;
using FullstackTemplate.Bff.Tests.Fakes;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

/// <summary>
/// The write rules of <see cref="BffTicketStore"/>, on the in-memory store. Each "request" is a scope of its own,
/// as in the app.
/// </summary>
public class BffTicketStoreTests
{
    private readonly TestClock _clock = new();
    private readonly InMemoryBffSessionStore _store;
    private readonly ContendedStore _contended;
    private readonly ServiceProvider _services;

    public BffTicketStoreTests()
    {
        _store = new InMemoryBffSessionStore(_clock);
        _contended = new ContendedStore(_store);
        _services = new ServiceCollection()
            .AddSingleton<IBffSessionStore>(_contended)
            .AddScoped<BffSessionRequest>()
            .BuildServiceProvider();
    }

    [Test]
    public async Task each_login_gets_a_new_key()
    {
        var ticketStore = TicketStore();

        var first = await ticketStore.StoreAsync(Ticket("rt-1"), NewRequest(), CancellationToken.None);
        var second = await ticketStore.StoreAsync(Ticket("rt-1"), NewRequest(), CancellationToken.None);

        first.ShouldNotBe(second);
        first.Length.ShouldBe(43);
    }

    [Test]
    public async Task a_read_gives_the_earlier_of_the_sliding_expiry_and_the_absolute_limit()
    {
        var ticketStore = TicketStore(sliding: TimeSpan.FromDays(14), absolute: TimeSpan.FromHours(1));
        var key = await ticketStore.StoreAsync(Ticket("rt-1"), NewRequest(), CancellationToken.None);

        var read = await ticketStore.RetrieveAsync(key, NewRequest(), CancellationToken.None);

        read.ShouldNotBeNull();
        read.Properties.ExpiresUtc.ShouldBe(_clock.Now.AddHours(1));
        read.Properties.GetTokenValue("refresh_token").ShouldBe("rt-1");
    }

    [Test]
    public async Task a_renewal_never_moves_the_expiry_past_the_absolute_limit()
    {
        var ticketStore = TicketStore(sliding: TimeSpan.FromHours(2), absolute: TimeSpan.FromHours(3));
        var key = await ticketStore.StoreAsync(Ticket("rt-1"), NewRequest(), CancellationToken.None);

        _clock.Advance(TimeSpan.FromHours(2));
        var request = NewRequest();
        var ticket = (await ticketStore.RetrieveAsync(key, request, CancellationToken.None))!;
        ticket.Properties.IssuedUtc = _clock.Now;
        ticket.Properties.ExpiresUtc = _clock.Now.AddHours(2);
        await ticketStore.RenewAsync(key, ticket, request, CancellationToken.None);

        var row = (await _store.GetAsync(key, CancellationToken.None))!;
        row.Expires.ShouldBe(row.AbsoluteExpires);
        row.Renewed.ShouldBe(_clock.Now);
    }

    [Test]
    public async Task an_expiry_renewal_that_loses_the_race_keeps_the_newer_tokens()
    {
        var ticketStore = TicketStore();
        var key = await ticketStore.StoreAsync(Ticket("rt-1"), NewRequest(), CancellationToken.None);
        var slide = NewRequest();
        var refresh = NewRequest();
        var slideTicket = (await ticketStore.RetrieveAsync(key, slide, CancellationToken.None))!;
        var refreshTicket = (await ticketStore.RetrieveAsync(key, refresh, CancellationToken.None))!;

        await ticketStore.RenewAsync(key, WithTokens(refreshTicket, "rt-2"), refresh, CancellationToken.None);
        _clock.Advance(TimeSpan.FromMinutes(5));
        slideTicket.Properties.IssuedUtc = _clock.Now;
        slideTicket.Properties.ExpiresUtc = _clock.Now.AddDays(14);
        await ticketStore.RenewAsync(key, slideTicket, slide, CancellationToken.None);

        var read = (await ticketStore.RetrieveAsync(key, NewRequest(), CancellationToken.None))!;
        read.Properties.GetTokenValue("refresh_token").ShouldBe("rt-2");
        read.Properties.ExpiresUtc.ShouldBe(_clock.Now.AddDays(14));
    }

    [Test]
    public async Task a_token_write_after_an_expiry_write_lands()
    {
        var ticketStore = TicketStore();
        var key = await ticketStore.StoreAsync(Ticket("rt-1"), NewRequest(), CancellationToken.None);
        var slide = NewRequest();
        var refresh = NewRequest();
        var slideTicket = (await ticketStore.RetrieveAsync(key, slide, CancellationToken.None))!;
        var refreshTicket = (await ticketStore.RetrieveAsync(key, refresh, CancellationToken.None))!;

        slideTicket.Properties.ExpiresUtc = _clock.Now.AddDays(1);
        await ticketStore.RenewAsync(key, slideTicket, slide, CancellationToken.None);
        await ticketStore.RenewAsync(key, WithTokens(refreshTicket, "rt-2"), refresh, CancellationToken.None);

        var read = (await ticketStore.RetrieveAsync(key, NewRequest(), CancellationToken.None))!;
        read.Properties.GetTokenValue("refresh_token").ShouldBe("rt-2");
    }

    [Test]
    public async Task a_token_write_that_loses_the_race_to_a_later_expiry_keeps_the_later_expiry()
    {
        var ticketStore = TicketStore();
        var key = await ticketStore.StoreAsync(Ticket("rt-1"), NewRequest(), CancellationToken.None);
        var slide = NewRequest();
        var refresh = NewRequest();
        var slideTicket = (await ticketStore.RetrieveAsync(key, slide, CancellationToken.None))!;
        var refreshTicket = (await ticketStore.RetrieveAsync(key, refresh, CancellationToken.None))!;

        slideTicket.Properties.IssuedUtc = _clock.Now.AddMinutes(5);
        slideTicket.Properties.ExpiresUtc = _clock.Now.AddDays(20);
        await ticketStore.RenewAsync(key, slideTicket, slide, CancellationToken.None);
        refreshTicket.Properties.IssuedUtc = _clock.Now;
        refreshTicket.Properties.ExpiresUtc = _clock.Now.AddDays(14);
        await ticketStore.RenewAsync(key, WithTokens(refreshTicket, "rt-2"), refresh, CancellationToken.None);

        var row = (await _store.GetAsync(key, CancellationToken.None))!;
        row.Expires.ShouldBe(_clock.Now.AddDays(20));
        row.Renewed.ShouldBe(_clock.Now.AddMinutes(5));
        var read = (await ticketStore.RetrieveAsync(key, NewRequest(), CancellationToken.None))!;
        read.Properties.GetTokenValue("refresh_token").ShouldBe("rt-2");
    }

    [Test]
    public async Task an_expiry_renewal_that_loses_the_race_to_a_later_expiry_does_not_move_it_back()
    {
        var ticketStore = TicketStore();
        var key = await ticketStore.StoreAsync(Ticket("rt-1"), NewRequest(), CancellationToken.None);
        var late = NewRequest();
        var early = NewRequest();
        var lateTicket = (await ticketStore.RetrieveAsync(key, late, CancellationToken.None))!;
        var earlyTicket = (await ticketStore.RetrieveAsync(key, early, CancellationToken.None))!;

        lateTicket.Properties.IssuedUtc = _clock.Now.AddMinutes(5);
        lateTicket.Properties.ExpiresUtc = _clock.Now.AddDays(20);
        await ticketStore.RenewAsync(key, lateTicket, late, CancellationToken.None);
        earlyTicket.Properties.IssuedUtc = _clock.Now.AddMinutes(1);
        earlyTicket.Properties.ExpiresUtc = _clock.Now.AddDays(15);
        await ticketStore.RenewAsync(key, earlyTicket, early, CancellationToken.None);

        var row = (await _store.GetAsync(key, CancellationToken.None))!;
        row.Expires.ShouldBe(_clock.Now.AddDays(20));
        row.Renewed.ShouldBe(_clock.Now.AddMinutes(5));
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task a_write_that_keeps_losing_the_race_stops_after_three_tries(bool newTokens)
    {
        var ticketStore = TicketStore();
        var key = await ticketStore.StoreAsync(Ticket("rt-1"), NewRequest(), CancellationToken.None);
        var request = NewRequest();
        var ticket = (await ticketStore.RetrieveAsync(key, request, CancellationToken.None))!;
        var before = (await _store.GetAsync(key, CancellationToken.None))!;

        _contended.Contend = 10;
        ticket.Properties.ExpiresUtc = _clock.Now.AddDays(20);
        if (newTokens)
            // The same refresh token, so that there is no new refresh token to revoke
            ticket.Properties.UpdateTokenValue("access_token", "at-2");
        await ticketStore.RenewAsync(key, ticket, request, CancellationToken.None);

        _contended.Writes.ShouldBe(3);
        var row = (await _store.GetAsync(key, CancellationToken.None))!;
        row.Expires.ShouldBe(before.Expires);
        var read = (await ticketStore.RetrieveAsync(key, NewRequest(), CancellationToken.None))!;
        read.Properties.GetTokenValue("access_token").ShouldBe("at-1");
    }

    [Test]
    public async Task a_token_write_after_another_token_write_fails_and_keeps_the_other_tokens()
    {
        // The refresh lock makes this case a fault: each token write runs inside it, after a reread of the row
        var ticketStore = TicketStore();
        var key = await ticketStore.StoreAsync(Ticket("rt-1"), NewRequest(), CancellationToken.None);
        var first = NewRequest();
        var second = NewRequest();
        var firstTicket = (await ticketStore.RetrieveAsync(key, first, CancellationToken.None))!;
        var secondTicket = (await ticketStore.RetrieveAsync(key, second, CancellationToken.None))!;

        await ticketStore.RenewAsync(key, WithTokens(firstTicket, "rt-2"), first, CancellationToken.None);
        await Should.ThrowAsync<InvalidOperationException>(() =>
            ticketStore.RenewAsync(key, WithTokens(secondTicket, "rt-3"), second, CancellationToken.None));

        var read = (await ticketStore.RetrieveAsync(key, NewRequest(), CancellationToken.None))!;
        read.Properties.GetTokenValue("refresh_token").ShouldBe("rt-2");
    }

    [Test]
    public async Task a_write_after_the_delete_does_not_bring_the_session_back()
    {
        var ticketStore = TicketStore();
        var key = await ticketStore.StoreAsync(Ticket("rt-1"), NewRequest(), CancellationToken.None);
        var late = NewRequest();
        var lateTicket = (await ticketStore.RetrieveAsync(key, late, CancellationToken.None))!;

        await ticketStore.RemoveAsync(key, NewRequest(), CancellationToken.None);
        // The same refresh token, so that there is no new refresh token to revoke
        lateTicket.Properties.UpdateTokenValue("access_token", "at-late");
        await ticketStore.RenewAsync(key, lateTicket, late, CancellationToken.None);
        lateTicket.Properties.ExpiresUtc = _clock.Now.AddDays(2);
        await ticketStore.RenewAsync(key, lateTicket, late, CancellationToken.None);

        (await _store.GetAsync(key, CancellationToken.None)).ShouldBeNull();
        (await ticketStore.RetrieveAsync(key, NewRequest(), CancellationToken.None)).ShouldBeNull();
    }

    [Test]
    public async Task a_refresh_with_no_id_token_keeps_the_stored_id_token()
    {
        var ticketStore = TicketStore();
        var key = await ticketStore.StoreAsync(Ticket("rt-1"), NewRequest(), CancellationToken.None);
        var request = NewRequest();
        var ticket = (await ticketStore.RetrieveAsync(key, request, CancellationToken.None))!;

        // As token management writes a refresh response with no id_token
        ticket.Properties.Items[".Token.id_token"] = null;
        await ticketStore.RenewAsync(key, WithTokens(ticket, "rt-2"), request, CancellationToken.None);

        var read = (await ticketStore.RetrieveAsync(key, NewRequest(), CancellationToken.None))!;
        read.Properties.GetTokenValue("refresh_token").ShouldBe("rt-2");
        read.Properties.GetTokenValue("id_token").ShouldBe("id-1");
    }

    [Test]
    public async Task a_renewal_of_a_key_that_the_request_did_not_read_writes_nothing()
    {
        var ticketStore = TicketStore();
        var key = await ticketStore.StoreAsync(Ticket("rt-1"), NewRequest(), CancellationToken.None);

        await ticketStore.RenewAsync(key, Ticket("rt-2"), NewRequest(), CancellationToken.None);

        var read = (await ticketStore.RetrieveAsync(key, NewRequest(), CancellationToken.None))!;
        read.Properties.GetTokenValue("refresh_token").ShouldBe("rt-1");
    }

    private BffTicketStore TicketStore(TimeSpan? sliding = null, TimeSpan? absolute = null) => new(
        new EphemeralDataProtectionProvider(),
        new FixedOptionsMonitor<BffSessionOptions>(new BffSessionOptions
        {
            SlidingExpiration = sliding ?? TimeSpan.FromDays(14),
            AbsoluteLifetime = absolute ?? TimeSpan.FromDays(30),
        }),
        _clock);

    private DefaultHttpContext NewRequest() => new() { RequestServices = _services.CreateScope().ServiceProvider };

    private AuthenticationTicket Ticket(string refreshToken)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user-1"), new Claim("sid", "sid-1")], "test"));
        var properties = new AuthenticationProperties
        {
            IssuedUtc = _clock.Now,
            ExpiresUtc = _clock.Now.AddDays(14),
        };
        properties.StoreTokens(
        [
            new AuthenticationToken { Name = "access_token", Value = "at-1" },
            new AuthenticationToken { Name = "refresh_token", Value = refreshToken },
            new AuthenticationToken { Name = "id_token", Value = "id-1" },
        ]);
        return new AuthenticationTicket(principal, properties, "Cookies");
    }

    /// <summary>
    /// A store in which another request writes the row just before each write of the test, while
    /// <see cref="Contend"/> is above zero. Each such write of the test thus finds a new version.
    /// </summary>
    private sealed class ContendedStore(InMemoryBffSessionStore inner) : IBffSessionStore
    {
        public int Contend { get; set; }

        public int Writes { get; private set; }

        public Task<BffSession> CreateAsync(BffSession session, CancellationToken cancellationToken) =>
            inner.CreateAsync(session, cancellationToken);

        public Task<BffSession?> GetAsync(string key, CancellationToken cancellationToken) => inner.GetAsync(key, cancellationToken);

        public async Task<BffSessionWrite> RenewAsync(string key, byte[] ticket, DateTimeOffset renewed, DateTimeOffset expires,
            uint expectedVersion, CancellationToken cancellationToken)
        {
            await ContendAsync(key, cancellationToken);
            return await inner.RenewAsync(key, ticket, renewed, expires, expectedVersion, cancellationToken);
        }

        public async Task<BffSessionWrite> RenewExpiryAsync(string key, DateTimeOffset renewed, DateTimeOffset expires,
            uint expectedVersion, CancellationToken cancellationToken)
        {
            await ContendAsync(key, cancellationToken);
            return await inner.RenewExpiryAsync(key, renewed, expires, expectedVersion, cancellationToken);
        }

        public Task<BffSession?> DeleteAsync(string key, CancellationToken cancellationToken) => inner.DeleteAsync(key, cancellationToken);

        public Task<IReadOnlyList<BffSession>> FindBySubjectAsync(string subjectId, CancellationToken cancellationToken) =>
            inner.FindBySubjectAsync(subjectId, cancellationToken);

        public Task<IReadOnlyList<BffSession>> FindBySessionIdAsync(string sessionId, CancellationToken cancellationToken) =>
            inner.FindBySessionIdAsync(sessionId, cancellationToken);

        public Task<int> DeleteEndedAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken) =>
            inner.DeleteEndedAsync(now, batchSize, cancellationToken);

        private async Task ContendAsync(string key, CancellationToken cancellationToken)
        {
            Writes++;
            if (Contend <= 0)
                return;

            Contend--;
            // The other write changes only the version. Its times are the same as the stored times.
            var current = (await inner.GetAsync(key, cancellationToken))!;
            await inner.RenewExpiryAsync(key, current.Renewed, current.Expires, current.Version, cancellationToken);
        }
    }

    private static AuthenticationTicket WithTokens(AuthenticationTicket ticket, string refreshToken)
    {
        ticket.Properties.UpdateTokenValue("access_token", $"at-for-{refreshToken}");
        ticket.Properties.UpdateTokenValue("refresh_token", refreshToken);
        return ticket;
    }
}
