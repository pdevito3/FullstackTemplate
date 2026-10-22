namespace FullstackTemplate.IntegrationTests.DPoP;

using FullstackTemplate.Server.Resources.Entrypoints.DPoP;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

public class DPoPReplayCacheTests : TestBase
{
    private static async Task<T> WithCacheAsync<T>(Func<DPoPReplayCache, Task<T>> action)
    {
        await using var scope = TestFixture.BaseScopeFactory.CreateAsyncScope();
        return await action(new DPoPReplayCache(scope.ServiceProvider.GetRequiredService<ISender>()));
    }

    [Test]
    public async Task the_same_key_cannot_use_a_jti_two_times()
    {
        var jkt = Guid.NewGuid().ToString("N");
        var expires = DateTimeOffset.UtcNow.AddMinutes(1);

        var first = await WithCacheAsync(c => c.TryAddAsync(jkt, "jti-1", expires, default));
        var second = await WithCacheAsync(c => c.TryAddAsync(jkt, "jti-1", expires, default));

        first.ShouldBeTrue();
        second.ShouldBeFalse();
    }

    [Test]
    public async Task another_key_can_use_the_same_jti()
    {
        var expires = DateTimeOffset.UtcNow.AddMinutes(1);

        var first = await WithCacheAsync(c => c.TryAddAsync(Guid.NewGuid().ToString("N"), "shared", expires, default));
        var second = await WithCacheAsync(c => c.TryAddAsync(Guid.NewGuid().ToString("N"), "shared", expires, default));

        first.ShouldBeTrue();
        second.ShouldBeTrue();
    }

    [Test]
    public async Task the_cleanup_deletes_the_expired_rows_and_keeps_the_others()
    {
        var jkt = Guid.NewGuid().ToString("N");
        // Long before the rows of the other tests expire: they use the real time or DPoPTestHost.Now. Thus this
        // cleanup cannot delete a row that a parallel replay test still needs.
        var now = DPoPTestHost.Now.AddYears(-10);
        await WithCacheAsync(c => c.TryAddAsync(jkt, "old", now.AddMinutes(-5), default));
        await WithCacheAsync(c => c.TryAddAsync(jkt, "new", now.AddMinutes(5), default));

        // Other tests can have expired rows too, so the number is at least one
        var deleted = await WithCacheAsync(c => c.DeleteExpiredAsync(now, 500, default));
        var oldAgain = await WithCacheAsync(c => c.TryAddAsync(jkt, "old", now.AddMinutes(5), default));
        var newAgain = await WithCacheAsync(c => c.TryAddAsync(jkt, "new", now.AddMinutes(5), default));

        deleted.ShouldBeGreaterThanOrEqualTo(1);
        oldAgain.ShouldBeTrue();
        newAgain.ShouldBeFalse();
    }
}
