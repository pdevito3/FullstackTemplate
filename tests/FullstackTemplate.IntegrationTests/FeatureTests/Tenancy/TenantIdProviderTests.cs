namespace FullstackTemplate.IntegrationTests.FeatureTests.Tenancy;

using FullstackTemplate.Server.Domain.Users.Features;
using FullstackTemplate.Server.Services;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using ZiggyCreatures.Caching.Fusion;

public class TenantIdProviderTests : TestBase
{
    [Fact]
    public async Task missing_user_is_cached()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var provider = testingServiceScope.GetService<ITenantIdProvider>();
        var identifier = $"later-{Guid.NewGuid()}";
        await testingServiceScope.StartGraph(out _)
            .AddUser(out var user)
            .BuildAsync();
        (await provider.GetTenantIdAsync(identifier)).ShouldBeNull();

        // A direct write raises no domain event, so nothing removes the cached miss.
        await testingServiceScope.ExecuteDbContextAsync(db => db.Database.ExecuteSqlRawAsync(
            "UPDATE users SET identifier = {0} WHERE id = {1}", identifier, user.Id));

        // Act
        var cached = await provider.GetTenantIdAsync(identifier);
        await provider.InvalidateCacheAsync(identifier);
        var fresh = await provider.GetTenantIdAsync(identifier);

        // Assert
        cached.ShouldBeNull();
        fresh.ShouldBe(user.TenantId);
    }

    [Fact]
    public async Task adding_a_user_removes_the_cached_miss()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var provider = testingServiceScope.GetService<ITenantIdProvider>();
        var identifier = $"new-{Guid.NewGuid()}";
        (await provider.GetTenantIdAsync(identifier)).ShouldBeNull();

        // Act
        await testingServiceScope.StartGraph(out _)
            .AddUser(out var user, u => u.Identifier = identifier)
            .BuildAsync();

        // Assert
        (await provider.GetTenantIdAsync(identifier)).ShouldBe(user.TenantId);
    }

    [Fact]
    public async Task deleting_a_user_removes_the_cached_tenant()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var provider = testingServiceScope.GetService<ITenantIdProvider>();
        await testingServiceScope.StartGraph(out _)
            .AddUser(out var user)
            .BuildAsync();
        (await provider.GetTenantIdAsync(user.Identifier)).ShouldBe(user.TenantId);

        // Act
        await testingServiceScope.SendAsync(new DeleteUser.Command(user.Id));

        // Assert
        (await provider.GetTenantIdAsync(user.Identifier)).ShouldBeNull();
    }

    [Fact]
    public async Task lookup_that_finishes_after_an_invalidation_does_not_cache_its_stale_result()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var scopeFactory = new HoldAfterReadScopeFactory(TestFixture.BaseScopeFactory);
        using var cache = new FusionCache(new FusionCacheOptions());
        var provider = new TenantIdProvider(cache, scopeFactory);
        await testingServiceScope.StartGraph(out _)
            .AddUser(out var user)
            .BuildAsync();

        // The lookup reads the user, then waits before it can write its result to the cache.
        var inFlight = Task.Run(() => provider.GetTenantIdAsync(user.Identifier));
        await scopeFactory.ReadDone.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await testingServiceScope.SendAsync(new DeleteUser.Command(user.Id));
        await provider.InvalidateCacheAsync(user.Identifier);

        // Act
        scopeFactory.Release.SetResult();
        (await inFlight).ShouldBe(user.TenantId);
        var afterDelete = await provider.GetTenantIdAsync(user.Identifier);

        // Assert
        afterDelete.ShouldBeNull();
    }

    /// <summary>
    /// The lookup disposes its scope after the read and before the cache write. This factory holds the
    /// first dispose until the test releases it.
    /// </summary>
    private sealed class HoldAfterReadScopeFactory(IServiceScopeFactory inner) : IServiceScopeFactory
    {
        public TaskCompletionSource ReadDone { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IServiceScope CreateScope() => new HeldScope(inner.CreateScope(), this);

        private sealed class HeldScope(IServiceScope scope, HoldAfterReadScopeFactory factory)
            : IServiceScope, IAsyncDisposable
        {
            public IServiceProvider ServiceProvider => scope.ServiceProvider;

            public void Dispose() => scope.Dispose();

            public async ValueTask DisposeAsync()
            {
                factory.ReadDone.TrySetResult();
                await factory.Release.Task;
                scope.Dispose();
            }
        }
    }
}
