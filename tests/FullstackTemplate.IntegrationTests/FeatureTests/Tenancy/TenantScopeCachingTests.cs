namespace FullstackTemplate.IntegrationTests.FeatureTests.Tenancy;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Services;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

public class TenantScopeCachingTests : TestBase
{
    [Test]
    public async Task context_reads_the_tenant_scope_once()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var tenantScopeProvider = Substitute.For<ITenantScopeProvider>();
        tenantScopeProvider.GetCurrentScope().Returns(TenantScope.ForTenant(TestFixture.DefaultTenant.Id));

        // Act
        await testingServiceScope.ExecuteScopeAsync(async services =>
        {
            await using var dbContext = CreateContext(services, tenantScopeProvider);

            // Two filtered entities in one query, then two more queries.
            await dbContext.Users.Where(u => dbContext.Tenants.Any(t => t.Id == u.TenantId)).CountAsync();
            await dbContext.Users.CountAsync();
            await dbContext.Tenants.CountAsync();
            return true;
        });

        // Assert
        tenantScopeProvider.Received(1).GetCurrentScope();
    }

    [Test]
    public async Task refresh_makes_the_context_read_the_tenant_scope_again()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var tenantScopeProvider = Substitute.For<ITenantScopeProvider>();
        tenantScopeProvider.GetCurrentScope().Returns(TenantScope.ForTenant(TestFixture.DefaultTenant.Id));

        // Act
        await testingServiceScope.ExecuteScopeAsync(async services =>
        {
            await using var dbContext = CreateContext(services, tenantScopeProvider);
            await dbContext.Users.CountAsync();
            dbContext.RefreshTenantScope();
            await dbContext.Users.CountAsync();
            return true;
        });

        // Assert
        tenantScopeProvider.Received(2).GetCurrentScope();
    }

    [Test]
    public async Task changing_the_test_caller_after_a_query_changes_the_rows_that_the_context_shows()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddUser(out var user)
            .BuildAsync();
        var foundAsUser = await testingServiceScope.ExecuteDbContextAsync(db => db.Users.AnyAsync(u => u.Id == user.Id));

        // Act
        testingServiceScope.SetUserUnauthenticated();
        var foundAsAnonymous = await testingServiceScope.ExecuteDbContextAsync(db => db.Users.AnyAsync(u => u.Id == user.Id));

        // Assert
        foundAsUser.ShouldBeTrue();
        foundAsAnonymous.ShouldBeFalse();
    }

    private static AppDbContext CreateContext(IServiceProvider services, ITenantScopeProvider tenantScopeProvider)
        => new(
            services.GetRequiredService<DbContextOptions<AppDbContext>>(),
            TimeProvider.System,
            services.GetRequiredService<ICurrentUserService>(),
            services.GetRequiredService<ICallerIdentityResolver>(),
            tenantScopeProvider,
            services.GetRequiredService<DomainEventDispatcher>());
}
