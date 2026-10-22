namespace FullstackTemplate.Bff.Tests.Integration;

using System.Net;
using System.Security.Cryptography;
using FullstackTemplate.Bff.BackChannel;
using FullstackTemplate.Bff.Sessions;
using FullstackTemplate.Bff.Tests.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Shouldly;

public class BackChannelLogoutTests
{
    private const string LogoutEvent = "http://schemas.openid.net/event/backchannel-logout";

    [Test]
    public async Task a_valid_logout_token_gets_200_with_no_store()
    {
        var response = await PostAsync(Token());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ShouldHaveNoStoreHeaders(response);
    }

    [Test]
    public async Task a_logout_token_with_only_a_sid_passes()
    {
        var response = await PostAsync(Token(c => c.Remove("sub")));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task a_logout_token_with_no_typ_passes()
    {
        var response = await PostAsync(Token(typ: null));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task a_request_with_no_form_gets_400_with_no_store()
    {
        using var browser = BffApp.Instance.CreateBrowser();

        var response = await browser.PostAsync("/bff/backchannel", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ShouldHaveNoStoreHeaders(response);
    }

    [Test]
    public async Task a_form_with_no_logout_token_gets_400_with_no_store()
    {
        using var browser = BffApp.Instance.CreateBrowser();

        var response = await browser.PostAsync("/bff/backchannel", new FormUrlEncodedContent([new("other", "x")]));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ShouldHaveNoStoreHeaders(response);
    }

    [Test]
    public async Task an_empty_logout_token_gets_400_with_no_store()
    {
        var response = await PostAsync("");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ShouldHaveNoStoreHeaders(response);
    }

    [Test]
    [MethodDataSource(nameof(MalformedForms))]
    public async Task a_malformed_form_gets_400_with_no_store(MalformedForm form)
    {
        using var browser = BffApp.Instance.CreateBrowser();

        var response = await browser.PostAsync("/bff/backchannel", form.Create());

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ShouldHaveNoStoreHeaders(response);
    }

    public static IEnumerable<Func<MalformedForm>> MalformedForms()
    {
        yield return () => new("multipart with no boundary", () =>
            new StringContent("logout_token=x", System.Text.Encoding.UTF8, "multipart/form-data"));
        // The form reader allows 1024 fields
        yield return () => new("too many fields", () =>
            new FormUrlEncodedContent(Enumerable.Range(0, 1100).Select(i => new KeyValuePair<string, string>($"f{i}", "x"))));
        // The form reader allows a key of 2048 characters
        yield return () => new("a key that is too long", () =>
            new FormUrlEncodedContent([new(new string('k', 3000), "x")]));
    }

    [Test]
    [MethodDataSource(nameof(InvalidTokens))]
    public async Task an_invalid_logout_token_gets_400_with_no_store(InvalidToken invalid)
    {
        var response = await PostAsync(invalid.Create());

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ShouldHaveNoStoreHeaders(response);
    }

    public static IEnumerable<Func<InvalidToken>> InvalidTokens()
    {
        yield return () => new("not a jwt", () => "not-a-jwt");
        yield return () => new("unsigned", () => Token(withoutSignature: true));
        yield return () => new("another key", () => Token(credentials: new SigningCredentials(
            new RsaSecurityKey(RSA.Create(2048)) { KeyId = "test-key" }, SecurityAlgorithms.RsaSha256)));
        yield return () => new("a key of the provider with another alg than the registered RS256",
            () => Token(credentials: BffApp.Instance.IdentityProvider.EcSigningCredentials));
        yield return () => new("another issuer", () => Token(c => c["iss"] = "https://other.example"));
        yield return () => new("another audience", () => Token(c => c["aud"] = "other-client"));
        yield return () => new("expired", () => Token(c => c["exp"] = Now() - 600));
        yield return () => new("no exp", () => Token(c => c.Remove("exp")));
        yield return () => new("no iat", () => Token(c => c.Remove("iat")));
        yield return () => new("an ID token typ", () => Token(typ: "JWT"));
        yield return () => new("no sub and no sid", () => Token(c => { c.Remove("sub"); c.Remove("sid"); }));
        yield return () => new("a nonce", () => Token(c => c["nonce"] = "n-1"));
        yield return () => new("no events", () => Token(c => c.Remove("events")));
        yield return () => new("an events claim that is not an object", () => Token(c => c["events"] = LogoutEvent));
        yield return () => new("another event", () => Token(c => c["events"] = new Dictionary<string, object> { ["other"] = new Dictionary<string, object>() }));
        yield return () => new("no jti", () => Token(c => c.Remove("jti")));
        yield return () => new("an empty jti", () => Token(c => c["jti"] = ""));
        yield return () => new("a jti that is too long", () => Token(c => c["jti"] = new string('j', 256)));
    }

    [Test]
    public async Task an_id_token_of_the_provider_is_not_a_logout_token()
    {
        var provider = BffApp.Instance.IdentityProvider;
        var subject = NewSubject();
        using var browser = BffApp.Instance.CreateBrowser();
        await BffApp.SignInAsync(browser, providerQuery: $"test_sub={subject}&test_sid={NewSessionId()}");

        var response = await PostAsync(provider.IssuedIdTokens(subject).Single());

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await browser.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task a_logout_token_with_a_sid_ends_only_the_sessions_of_that_sid()
    {
        var app = BffApp.Instance;
        var subject = NewSubject();
        var sessionId = NewSessionId();
        using var ended = app.CreateBrowser();
        using var otherIdpSession = app.CreateBrowser();
        var endedKey = app.SessionKeyIn(await BffApp.SignInAsync(ended, providerQuery: $"test_sub={subject}&test_sid={sessionId}"))!;
        var otherKey = app.SessionKeyIn(await BffApp.SignInAsync(otherIdpSession, providerQuery: $"test_sub={subject}&test_sid={NewSessionId()}"))!;

        var response = await PostAsync(Token(c => { c["sub"] = subject; c["sid"] = sessionId; }));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await app.WithStoreAsync(store => store.GetAsync(endedKey, CancellationToken.None))).ShouldBeNull();
        (await ended.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await app.WithStoreAsync(store => store.GetAsync(otherKey, CancellationToken.None))).ShouldNotBeNull();
        (await otherIdpSession.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task a_logout_token_with_only_a_sid_ends_the_session_of_that_sid()
    {
        var app = BffApp.Instance;
        var sessionId = NewSessionId();
        using var browser = app.CreateBrowser();
        var key = app.SessionKeyIn(await BffApp.SignInAsync(browser, providerQuery: $"test_sub={NewSubject()}&test_sid={sessionId}"))!;

        var response = await PostAsync(Token(c => { c.Remove("sub"); c["sid"] = sessionId; }));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await app.WithStoreAsync(store => store.GetAsync(key, CancellationToken.None))).ShouldBeNull();
        (await browser.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task a_logout_token_with_only_a_sub_ends_all_the_sessions_of_that_sub()
    {
        var app = BffApp.Instance;
        var subject = NewSubject();
        var otherSubject = NewSubject();
        using var first = app.CreateBrowser();
        using var second = app.CreateBrowser();
        using var otherUser = app.CreateBrowser();
        await BffApp.SignInAsync(first, providerQuery: $"test_sub={subject}&test_sid={NewSessionId()}");
        await BffApp.SignInAsync(second, providerQuery: $"test_sub={subject}");
        await BffApp.SignInAsync(otherUser, providerQuery: $"test_sub={otherSubject}");

        var response = await PostAsync(Token(c => { c["sub"] = subject; c.Remove("sid"); }));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await app.WithStoreAsync(store => store.FindBySubjectAsync(subject, CancellationToken.None))).ShouldBeEmpty();
        (await first.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await second.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await otherUser.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task a_back_channel_logout_sends_no_revocation_request_to_the_identity_provider()
    {
        var app = BffApp.Instance;
        var subject = NewSubject();
        var sessionId = NewSessionId();
        using var first = app.CreateBrowser();
        using var second = app.CreateBrowser();
        await BffApp.SignInAsync(first, providerQuery: $"test_sub={subject}&test_sid={sessionId}");
        await BffApp.SignInAsync(second, providerQuery: $"test_sub={subject}");

        (await PostAsync(Token(c => { c["sub"] = subject; c["sid"] = sessionId; }))).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await PostAsync(Token(c => { c["sub"] = subject; c.Remove("sid"); }))).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await app.WithStoreAsync(store => store.FindBySubjectAsync(subject, CancellationToken.None))).ShouldBeEmpty();
        (await first.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await second.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        app.IdentityProvider.IssuedRefreshTokens(subject).Count.ShouldBe(2);
        app.IdentityProvider.WasRefreshTokenRevoked(subject).ShouldBeFalse();
    }

    [Test]
    public async Task a_replayed_logout_token_gets_400_and_ends_no_new_session()
    {
        var app = BffApp.Instance;
        var subject = NewSubject();
        var jti = Guid.NewGuid().ToString();
        var logoutToken = Token(c => { c["sub"] = subject; c.Remove("sid"); c["jti"] = jti; });
        (await PostAsync(logoutToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        using var browser = app.CreateBrowser();
        await BffApp.SignInAsync(browser, providerQuery: $"test_sub={subject}");

        var replay = await PostAsync(logoutToken);

        replay.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ShouldHaveNoStoreHeaders(replay);
        (await browser.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.OK);
        var used = await UsedLogoutTokenAsync(jti);
        used.ShouldNotBeNull();
        used.Expires.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddSeconds(120));
    }

    [Test]
    public async Task the_same_logout_token_sent_twice_at_the_same_time_passes_one_time()
    {
        var logoutToken = Token(c => { c["sub"] = NewSubject(); c["sid"] = NewSessionId(); });

        var responses = await Task.WhenAll(PostAsync(logoutToken), PostAsync(logoutToken));

        responses.Select(r => r.StatusCode).ShouldBe([HttpStatusCode.OK, HttpStatusCode.BadRequest], ignoreOrder: true);
    }

    [Test]
    public async Task two_inserts_of_the_same_jti_at_the_same_time_let_exactly_one_win()
    {
        var jti = Guid.NewGuid().ToString();
        var expires = DateTimeOffset.UtcNow.AddMinutes(5);
        await using var first = NewDbContext();
        await using var second = NewDbContext();
        await second.Database.OpenConnectionAsync();
        var secondProcessId = ((NpgsqlConnection)second.Database.GetDbConnection()).ProcessID;

        await using var transaction = await first.Database.BeginTransactionAsync();
        (await new LogoutTokenReplayCache(first).TryAddAsync(jti, expires, CancellationToken.None)).ShouldBeTrue();
        var secondAdd = new LogoutTokenReplayCache(second).TryAddAsync(jti, expires, CancellationToken.None);

        // The second insert is in the database and waits for the uncommitted row of the first insert
        await WaitUntilAsync(() => IsWaitingOnALockAsync(secondProcessId));
        secondAdd.IsCompleted.ShouldBeFalse();
        await transaction.CommitAsync();

        (await secondAdd).ShouldBeFalse();
        await using var check = NewDbContext();
        (await check.UsedLogoutTokens.CountAsync(t => t.Jti == jti)).ShouldBe(1);
    }

    [Test]
    public async Task a_failed_write_of_the_jti_gets_400_and_ends_no_session()
    {
        await using var app = BffApp.StartAnotherInstance(services: services => services.ConfigureDbContext<BffDbContext>(
            options => options.AddInterceptors(new FailingCommandInterceptor("bff_used_logout_tokens.add"))));
        var subject = NewSubject();
        var jti = Guid.NewGuid().ToString();
        var logoutToken = Token(c => { c["sub"] = subject; c.Remove("sid"); c["jti"] = jti; });
        using var browser = app.CreateBrowser();
        await BffApp.SignInAsync(browser, providerQuery: $"test_sub={subject}");

        var response = await PostAsync(app, logoutToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ShouldHaveNoStoreHeaders(response);
        (await app.WithStoreAsync(store => store.FindBySubjectAsync(subject, CancellationToken.None))).Count.ShouldBe(1);
        (await browser.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.OK);
        app.IdentityProvider.WasRefreshTokenRevoked(subject).ShouldBeFalse();
        (await UsedLogoutTokenAsync(jti)).ShouldBeNull();

        // The identity provider sends the token again to a BFF that can write the jti
        (await PostAsync(logoutToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await browser.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task a_failed_delete_of_a_session_gets_500_and_lets_the_same_token_pass_again()
    {
        await using var app = BffApp.StartAnotherInstance(services: services => services.ConfigureDbContext<BffDbContext>(
            options => options.AddInterceptors(new FailingCommandInterceptor("bff_sessions.delete"))));
        var subject = NewSubject();
        var jti = Guid.NewGuid().ToString();
        var logoutToken = Token(c => { c["sub"] = subject; c.Remove("sid"); c["jti"] = jti; });
        using var browser = app.CreateBrowser();
        await BffApp.SignInAsync(browser, providerQuery: $"test_sub={subject}");

        var response = await PostAsync(app, logoutToken);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        (await browser.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await UsedLogoutTokenAsync(jti)).ShouldBeNull();

        // The identity provider sends the token again to a BFF that can delete the row
        (await PostAsync(logoutToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await browser.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task a_request_that_aborts_during_the_logout_still_ends_all_the_sessions()
    {
        var hold = new HoldingCommandInterceptor("bff_sessions.delete");
        await using var app = BffApp.StartAnotherInstance(services: services => services.ConfigureDbContext<BffDbContext>(
            options => options.AddInterceptors(hold)));
        var subject = NewSubject();
        using var first = app.CreateBrowser();
        using var second = app.CreateBrowser();
        await BffApp.SignInAsync(first, providerQuery: $"test_sub={subject}&test_sid={NewSessionId()}");
        await BffApp.SignInAsync(second, providerQuery: $"test_sub={subject}");
        using var abort = new CancellationTokenSource();

        var post = PostAsync(app, Token(c => { c["sub"] = subject; c.Remove("sid"); }), abort.Token);
        await hold.Gate.Arrived.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await abort.CancelAsync();
        hold.Gate.Released.SetResult();
        (await Should.ThrowAsync<Exception>(post)).ShouldBeAssignableTo<OperationCanceledException>();

        await WaitUntilAsync(async () =>
            (await app.WithStoreAsync(store => store.FindBySubjectAsync(subject, CancellationToken.None))).Count == 0);
        (await first.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await second.GetApiAsync("/bff/user")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task a_rejected_logout_token_is_not_kept_in_the_replay_cache()
    {
        var jti = Guid.NewGuid().ToString();

        (await PostAsync(Token(c => { c["jti"] = jti; c["nonce"] = "n-1"; }))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await UsedLogoutTokenAsync(jti)).ShouldBeNull();
        (await PostAsync(Token(c => c["jti"] = jti))).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    public static string Token(Action<Dictionary<string, object>>? change = null, string? typ = "logout+jwt",
        SigningCredentials? credentials = null, bool withoutSignature = false)
    {
        var provider = BffApp.Instance.IdentityProvider;
        var claims = new Dictionary<string, object>
        {
            ["iss"] = provider.Issuer,
            ["aud"] = FakeIdentityProvider.ClientId,
            ["iat"] = Now(),
            ["exp"] = Now() + 120,
            ["jti"] = Guid.NewGuid().ToString(),
            ["sub"] = "user-backchannel",
            ["sid"] = "sid-backchannel",
            ["events"] = new Dictionary<string, object> { [LogoutEvent] = new Dictionary<string, object>() },
        };
        change?.Invoke(claims);
        return provider.CreateJwt(claims, typ, credentials, withoutSignature);
    }

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static string NewSubject() => $"user-backchannel-{Guid.NewGuid():N}";

    private static string NewSessionId() => $"sid-{Guid.NewGuid():N}";

    private static async Task<UsedLogoutToken?> UsedLogoutTokenAsync(string jti)
    {
        await using var scope = BffApp.Instance.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<BffDbContext>().UsedLogoutTokens.AsNoTracking()
            .SingleOrDefaultAsync(t => t.Jti == jti);
    }

    public static Task<HttpResponseMessage> PostAsync(string logoutToken) => PostAsync(BffApp.Instance, logoutToken);

    private static async Task<HttpResponseMessage> PostAsync(BffApp app, string logoutToken,
        CancellationToken cancellationToken = default)
    {
        using var client = app.CreateBrowser();
        return await client.PostAsync("/bff/backchannel", new FormUrlEncodedContent([new("logout_token", logoutToken)]),
            cancellationToken);
    }

    /// <summary>
    /// A context on <c>bffdb</c> with no retries, so that a test can open its own transaction.
    /// </summary>
    private static BffDbContext NewDbContext()
    {
        var options = new DbContextOptionsBuilder<BffDbContext>();
        BffSessionExtensions.ConfigureBffDb(options, BffApp.Database.ConnectionString);
        return new BffDbContext(options.Options);
    }

    private static async Task<bool> IsWaitingOnALockAsync(int processId)
    {
        await using var connection = new NpgsqlConnection(BffApp.Database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT wait_event_type FROM pg_stat_activity WHERE pid = @pid", connection);
        command.Parameters.AddWithValue("pid", processId);
        return await command.ExecuteScalarAsync() is "Lock";
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (!await condition())
        {
            if (DateTimeOffset.UtcNow > deadline)
                throw new TimeoutException("The condition did not become true in 10 seconds.");
            await Task.Delay(20);
        }
    }

    private static void ShouldHaveNoStoreHeaders(HttpResponseMessage response)
    {
        response.Headers.CacheControl!.NoCache.ShouldBeTrue();
        response.Headers.CacheControl.NoStore.ShouldBeTrue();
        response.Headers.Pragma.ToString().ShouldBe("no-cache");
    }

    public sealed record MalformedForm(string Name, Func<HttpContent> Create)
    {
        public override string ToString() => Name;
    }

    public sealed record InvalidToken(string Name, Func<string> Create)
    {
        public override string ToString() => Name;
    }
}
