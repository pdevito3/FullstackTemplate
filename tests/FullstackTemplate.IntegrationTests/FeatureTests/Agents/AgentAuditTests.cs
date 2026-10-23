namespace FullstackTemplate.IntegrationTests.FeatureTests.Agents;

using FullstackTemplate.Server.Domain.Tenants.Dtos;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Fakes.Tenant;
using Microsoft.EntityFrameworkCore;
using Shouldly;

/// <summary>
/// Each write records the <c>sub</c> and the client of the token, so a change shows which agent made it and for
/// which user.
/// </summary>
public class AgentAuditTests : TestBase
{
    [Test]
    public async Task change_through_a_delegated_agent_records_the_user_and_the_agent()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUser(out var admin, u => u.Role = "Admin")
            .Under(tenant)
            .AddAgent(out var agent, a => a.Permissions = [Permission.Names.ManageTenant])
            .BuildAsync();
        testingServiceScope.SetDelegatedClient(admin.Identifier, agent.ClientId);

        // Act
        await testingServiceScope.SendAsync(new UpdateTenant.Command(tenant.Id, await NewTenantNameAsync(tenant.Id)));

        // Assert
        (await LastModifiedOfAsync(tenant.Id)).ShouldBe((admin.Identifier, agent.ClientId));
    }

    [Test]
    public async Task change_by_a_machine_agent_records_its_client_and_its_service_account()
    {
        // Arrange
        const string serviceAccount = "service-account-fleet-sync";
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddTenant(out var tenant)
            .AddAgent(out var machineAgent, a =>
            {
                a.Mode = "machine";
                a.Permissions = [Permission.Names.ManageTenant];
            })
            .BuildAsync();
        testingServiceScope.SetMachineClient(machineAgent.ClientId, serviceAccount);

        // Act
        await testingServiceScope.SendAsync(new UpdateTenant.Command(tenant.Id, await NewTenantNameAsync(tenant.Id)));

        // Assert
        (await LastModifiedOfAsync(tenant.Id)).ShouldBe((serviceAccount, machineAgent.ClientId));
    }

    [Test]
    public async Task change_by_a_user_of_the_app_records_the_user_and_the_app()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUser(out var admin, u => u.Role = "Admin")
            .BuildAsync();
        testingServiceScope.SetUserWithDefaults(identifier: admin.Identifier);

        // Act
        await testingServiceScope.SendAsync(new UpdateTenant.Command(tenant.Id, await NewTenantNameAsync(tenant.Id)));

        // Assert
        (await LastModifiedOfAsync(tenant.Id)).ShouldBe((admin.Identifier, TestFixture.FirstPartyClientId));
    }

    private static async Task<TenantForUpdateDto> NewTenantNameAsync(string tenantId)
    {
        var version = await new TestingServiceScope().ExecuteDbContextAsync(db => db.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.Id == tenantId)
            .Select(t => t.Version)
            .SingleAsync());
        return new TenantForUpdateDto { Name = new FakeTenantForCreationDto().Generate().Name, Version = version };
    }

    private static async Task<(string? By, string? ByClient)> LastModifiedOfAsync(string tenantId)
    {
        var tenant = await new TestingServiceScope().ExecuteDbContextAsync(db => db.Tenants
            .IgnoreQueryFilters()
            .SingleAsync(t => t.Id == tenantId));
        return (tenant.LastModifiedBy, tenant.LastModifiedByClient);
    }
}
