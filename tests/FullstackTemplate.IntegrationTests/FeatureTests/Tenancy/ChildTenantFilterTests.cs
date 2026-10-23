namespace FullstackTemplate.IntegrationTests.FeatureTests.Tenancy;

using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Resources;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Microsoft.EntityFrameworkCore;
using Shouldly;

public class ChildTenantFilterTests : TestBase
{
    [Test]
    public async Task caller_of_one_tenant_sees_no_permission_row_of_another_tenant()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddTenant(out var otherTenant)
            .AddUser(out var otherUser, u => u.Role = "Admin")
            .Under(otherTenant)
            .AddAgent(out var otherAgent, a => a.Permissions = [Permission.Names.ManageTenant])
            .BuildAsync();
        await testingServiceScope.SetupTenantAsync();

        // Act
        var userPermissionsOfAllTenants = await testingServiceScope.ExecuteDbContextAsync(db =>
            db.UserPermissions.IgnoreQueryFilters([QueryFilterNames.Tenant]).CountAsync(p => p.UserId == otherUser.Id));
        var agentPermissionsOfAllTenants = await testingServiceScope.ExecuteDbContextAsync(db =>
            db.AgentPermissions.IgnoreQueryFilters([QueryFilterNames.Tenant]).CountAsync(p => p.AgentId == otherAgent.Id));
        var userPermissions = await testingServiceScope.ExecuteDbContextAsync(db =>
            db.UserPermissions.Where(p => p.UserId == otherUser.Id).ToListAsync());
        var agentPermissions = await testingServiceScope.ExecuteDbContextAsync(db =>
            db.AgentPermissions.Where(p => p.AgentId == otherAgent.Id).ToListAsync());

        // Assert
        userPermissionsOfAllTenants.ShouldBeGreaterThan(0);
        agentPermissionsOfAllTenants.ShouldBe(1);
        userPermissions.ShouldBeEmpty();
        agentPermissions.ShouldBeEmpty();
    }
}
