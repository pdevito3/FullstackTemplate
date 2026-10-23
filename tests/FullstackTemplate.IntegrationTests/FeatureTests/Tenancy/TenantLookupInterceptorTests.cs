namespace FullstackTemplate.IntegrationTests.FeatureTests.Tenancy;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using static DomainEvents.DomainEventTestServices;

public class TenantLookupInterceptorTests : TestBase
{
    [Test]
    public async Task cache_failure_after_the_commit_does_not_fail_the_save()
    {
        // Arrange
        var cache = new FailingInvalidation(TestFixture.DefaultTenant.Id);
        await using var services = TestFixture.BuildServiceProvider(s => s.AddSingleton<ITenantIdProvider>(cache));
        var scope = new TestingServiceScope(services);
        var db = scope.GetService<AppDbContext>();
        var identifier = $"new-{Guid.NewGuid()}";

        // Act
        db.Users.Add(NewUser(TestFixture.DefaultTenant.Id, identifier));
        await db.SaveChangesAsync();

        // Assert
        cache.Invalidations.ShouldBe([identifier]);
        (await ReadAsync(read => read.Users.CountAsync(u => u.Identifier == identifier))).ShouldBe(1);
    }

    private sealed class FailingInvalidation(string tenantId) : ITenantIdProvider
    {
        public List<string> Invalidations { get; } = [];

        public Task<string?> GetTenantIdAsync(string userIdentifier, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(tenantId);

        public Task InvalidateCacheAsync(string userIdentifier, CancellationToken cancellationToken = default)
        {
            Invalidations.Add(userIdentifier);
            throw new InvalidOperationException("The cache is down.");
        }
    }
}
