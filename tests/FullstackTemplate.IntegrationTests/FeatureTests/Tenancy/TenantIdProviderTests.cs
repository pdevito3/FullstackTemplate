namespace FullstackTemplate.IntegrationTests.FeatureTests.Tenancy;

using FullstackTemplate.Server.Domain.Users.Features;
using FullstackTemplate.Server.Services;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Microsoft.EntityFrameworkCore;
using Shouldly;

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
}
