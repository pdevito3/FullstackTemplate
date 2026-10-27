namespace FullstackTemplate.IntegrationTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Microsoft.EntityFrameworkCore;
using Shouldly;

public class SoftDeleteTests : TestBase
{
    [Test]
    public async Task soft_delete_of_a_tenant_keeps_the_tenant_of_its_loaded_user()
    {
        // Arrange
        await new TestingServiceScope().StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUser(out var user)
            .BuildAsync();
        var scope = new TestingServiceScope();
        scope.SetUserWithDefaults(identifier: user.Identifier);
        var dbContext = scope.GetService<AppDbContext>();
        var loadedTenant = await dbContext.Tenants.SingleAsync(t => t.Id == tenant.Id);
        var loadedUser = await dbContext.Users.SingleAsync(u => u.Id == user.Id);

        // Act
        dbContext.Tenants.Remove(loadedTenant);
        await dbContext.SaveChangesAsync();

        // Assert
        loadedUser.TenantId.ShouldBe(tenant.Id);
        var saved = await new TestingServiceScope().ExecuteDbContextAsync(async db => new
        {
            Tenant = await db.Tenants.IgnoreQueryFilters().SingleAsync(t => t.Id == tenant.Id),
            User = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == user.Id),
        });
        saved.Tenant.IsDeleted.ShouldBeTrue();
        saved.User.IsDeleted.ShouldBeFalse();
        saved.User.TenantId.ShouldBe(tenant.Id);
        saved.User.Version.ShouldBe(user.Version);
    }
}
