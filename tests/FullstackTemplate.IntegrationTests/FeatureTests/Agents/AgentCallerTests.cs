namespace FullstackTemplate.IntegrationTests.FeatureTests.Agents;

using FullstackTemplate.Server.Domain.Agents.Features;
using FullstackTemplate.Server.Domain.Tenants.Dtos;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.Features;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Fakes.Agent;
using FullstackTemplate.SharedTestHelpers.Fakes.Tenant;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using Microsoft.EntityFrameworkCore;
using Shouldly;

/// <summary>
/// A delegated agent holds what both its user and its grant hold. A machine agent holds its grant, in the tenant of
/// its record. Each check reads the rows again, so a revoke or a delete stops the agent on its next check.
/// </summary>
public class AgentCallerTests : TestBase
{
    private const string UnknownClientDenial =
        "This client is not a registered agent of your tenant. A tenant admin can register it.";

    [Test]
    public async Task admin_through_an_agent_granted_only_manage_tenant_cannot_do_manage_users_work()
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
        var renamed = await testingServiceScope.SendAsync(new UpdateTenant.Command(tenant.Id, await NewTenantNameAsync(tenant.Id)));
        var addUser = () => testingServiceScope.SendAsync(new AddUser.Command(new FakeUserForCreationDto().Generate()));

        // Assert
        renamed.Id.ShouldBe(tenant.Id);
        (await addUser.ShouldThrowAsync<ForbiddenAccessException>()).Message.ShouldBe(
            $"The agent {agent.ClientId} is not granted the manage_users permission. A tenant admin can grant it.");
    }

    [Test]
    public async Task member_through_an_agent_granted_manage_tenant_cannot_do_what_the_member_cannot()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUser(out var member, u => u.Role = "User")
            .Under(tenant)
            .AddAgent(out var agent, a => a.Permissions = [Permission.Names.ManageTenant])
            .BuildAsync();
        testingServiceScope.SetDelegatedClient(member.Identifier, agent.ClientId);
        var update = await NewTenantNameAsync(tenant.Id);

        // Act
        var act = () => testingServiceScope.SendAsync(new UpdateTenant.Command(tenant.Id, update));

        // Assert
        (await act.ShouldThrowAsync<ForbiddenAccessException>()).Message
            .ShouldBe("You need the manage_tenant permission to do this.");
    }

    [Test]
    public async Task agent_of_another_tenant_is_an_unknown_client_for_a_user_of_this_tenant()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _).AddTenant(out _).AddAgent(out var otherAgent).BuildAsync();
        testingServiceScope.SetDelegatedClient(TestFixture.DefaultUser.Identifier, otherAgent.ClientId);
        var update = await NewTenantNameAsync(TestFixture.DefaultTenant.Id);

        // Act
        var tenants = await testingServiceScope.SendAsync(new GetTenantList.Query(AllTenantsPage()));
        var rename = () => testingServiceScope.SendAsync(new UpdateTenant.Command(TestFixture.DefaultTenant.Id, update));

        // Assert
        tenants.ShouldBeEmpty();
        (await rename.ShouldThrowAsync<ForbiddenAccessException>()).Message.ShouldBe(UnknownClientDenial);
    }

    [Test]
    public async Task revoked_grant_denies_the_next_request()
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
        await testingServiceScope.SendAsync(new UpdateTenant.Command(tenant.Id, await NewTenantNameAsync(tenant.Id)));

        var tenantAdmin = new TestingServiceScope();
        tenantAdmin.SetUserWithDefaults(identifier: admin.Identifier);
        await tenantAdmin.SendAsync(new RemoveAgentPermission.Command(agent.Id, Permission.Names.ManageTenant));

        var nextRequest = new TestingServiceScope();
        nextRequest.SetDelegatedClient(admin.Identifier, agent.ClientId);
        var update = await NewTenantNameAsync(tenant.Id);

        // Act
        var act = () => nextRequest.SendAsync(new UpdateTenant.Command(tenant.Id, update));

        // Assert
        (await act.ShouldThrowAsync<ForbiddenAccessException>()).Message.ShouldBe(
            $"The agent {agent.ClientId} is not granted the manage_tenant permission. A tenant admin can grant it.");
    }

    [Test]
    public async Task machine_agent_reads_only_the_tenant_of_its_record()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddTenant(out var tenantA)
            .AddAgent(out var machineAgent, a => a.Mode = "machine")
            .BuildAsync();
        await testingServiceScope.StartGraph(out _).AddTenant(out var tenantB).BuildAsync();
        testingServiceScope.SetMachineClient(machineAgent.ClientId, serviceAccountSubject: "service-account-fleet-sync");

        // Act
        var tenants = await testingServiceScope.SendAsync(new GetTenantList.Query(AllTenantsPage()));
        var getTenantB = () => testingServiceScope.SendAsync(new GetTenant.Query(tenantB.Id));

        // Assert
        tenants.Select(t => t.Id).ShouldBe([tenantA.Id]);
        await getTenantB.ShouldThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task tenant_that_gives_a_user_the_service_account_of_a_machine_agent_cannot_take_the_machine()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddTenant(out var tenantA)
            .AddAgent(out var machineAgent, a => a.Mode = "machine")
            .BuildAsync();
        var serviceAccount = $"service-account-{machineAgent.ClientId}";
        var tenantBAdmin = new TestingServiceScope();
        await tenantBAdmin.SendAsync(new AddUser.Command(
            new FakeUserForCreationDto().Generate() with { Identifier = serviceAccount }));

        // Act
        var register = () => tenantBAdmin.SendAsync(new AddAgent.Command(
            new FakeAgentForCreationDto().Generate() with { ClientId = machineAgent.ClientId, Mode = "delegated" }));
        var conflict = await register.ShouldThrowAsync<ConflictException>();

        var machine = new TestingServiceScope();
        machine.SetMachineClient(machineAgent.ClientId, serviceAccountSubject: serviceAccount);
        var tenants = await machine.SendAsync(new GetTenantList.Query(AllTenantsPage()));

        // Assert
        conflict.Property.ShouldBe("clientId");
        tenants.Select(t => t.Id).ShouldBe([tenantA.Id]);
    }

    [Test]
    public async Task machine_agent_does_the_work_of_its_grant_in_its_tenant()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddTenant(out var tenant)
            .AddAgent(out var machineAgent, a =>
            {
                a.Mode = "machine";
                a.Permissions = [Permission.Names.ManageTenant];
            })
            .BuildAsync();
        testingServiceScope.SetMachineClient(machineAgent.ClientId);
        var name = await NewTenantNameAsync(tenant.Id);

        // Act
        var renamed = await testingServiceScope.SendAsync(new UpdateTenant.Command(tenant.Id, name));
        var addUser = () => testingServiceScope.SendAsync(new AddUser.Command(new FakeUserForCreationDto().Generate()));

        // Assert
        renamed.Name.ShouldBe(name.Name);
        (await addUser.ShouldThrowAsync<ForbiddenAccessException>()).Message.ShouldBe(
            $"The agent {machineAgent.ClientId} is not granted the manage_users permission. A tenant admin can grant it.");
    }

    [Test]
    public async Task machine_agent_of_a_deleted_tenant_sees_no_rows_and_holds_no_permission()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddTenant(out var tenant)
            .AddAgent(out var machineAgent, a =>
            {
                a.Mode = "machine";
                a.Permissions = [Permission.Names.ManageTenant];
            })
            .BuildAsync();
        testingServiceScope.SetMachineClient(TestFixture.TrustedMachineClientId);
        await testingServiceScope.SendAsync(new DeleteTenant.Command(tenant.Id));

        var nextRequest = new TestingServiceScope();
        nextRequest.SetMachineClient(machineAgent.ClientId);
        var update = await NewTenantNameAsync(tenant.Id);

        // Act
        var tenants = await nextRequest.SendAsync(new GetTenantList.Query(AllTenantsPage()));
        var rename = () => nextRequest.SendAsync(new UpdateTenant.Command(tenant.Id, update));

        // Assert
        tenants.ShouldBeEmpty();
        (await rename.ShouldThrowAsync<ForbiddenAccessException>()).Message.ShouldBe(UnknownClientDenial);
    }

    [Test]
    public async Task agent_deleted_after_its_identity_resolved_holds_no_permission()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddTenant(out var tenant)
            .AddAgent(out var machineAgent, a =>
            {
                a.Mode = "machine";
                a.Permissions = [Permission.Names.ManageTenant];
            })
            .BuildAsync();
        testingServiceScope.SetMachineClient(machineAgent.ClientId);
        await testingServiceScope.SendAsync(new UpdateTenant.Command(tenant.Id, await NewTenantNameAsync(tenant.Id)));

        var operatorScope = new TestingServiceScope();
        operatorScope.SetMachineClient(TestFixture.TrustedMachineClientId);
        await operatorScope.SendAsync(new DeleteAgent.Command(machineAgent.Id));
        var update = await NewTenantNameAsync(tenant.Id);

        // Act
        var act = () => testingServiceScope.SendAsync(new UpdateTenant.Command(tenant.Id, update));

        // Assert
        (await act.ShouldThrowAsync<ForbiddenAccessException>()).Message.ShouldBe(UnknownClientDenial);
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

    private static TenantParametersDto AllTenantsPage() => TenantParametersDto.From(null, 500, null, null);
}
