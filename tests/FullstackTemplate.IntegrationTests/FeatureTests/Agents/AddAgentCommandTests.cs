namespace FullstackTemplate.IntegrationTests.FeatureTests.Agents;

using FullstackTemplate.IntegrationTests.Databases;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.Agents.Features;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Fakes.Agent;
using FullstackTemplate.SharedTestHelpers.Utilities;
using Microsoft.EntityFrameworkCore;
using Shouldly;

/// <summary>
/// A tenant admin registers a delegated agent in their own tenant. Only a caller that manages all tenants binds a
/// machine agent. A client ID has at most one live agent in a tenant, and a new agent cannot take a client ID that has a
/// live machine agent. The client of the app and the clients on the allow-lists cannot be agents.
/// </summary>
public class AddAgentCommandTests : TestBase
{
    [Test]
    public async Task tenant_admin_adds_a_delegated_agent_with_its_first_grant()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var dto = new FakeAgentForCreationDto().Generate() with { Permissions = [Permission.Names.ManageTenant] };

        // Act
        var agentReturned = await testingServiceScope.SendAsync(new AddAgent.Command(dto));

        // Assert
        agentReturned.TenantId.ShouldBe(TestFixture.DefaultTenant.Id);
        agentReturned.Name.ShouldBe(dto.Name);
        agentReturned.ClientId.ShouldBe(dto.ClientId);
        agentReturned.Mode.ShouldBe("delegated");
        agentReturned.Permissions.ShouldBe([Permission.Names.ManageTenant]);

        var agentFromGet = await new TestingServiceScope().SendAsync(new GetAgent.Query(agentReturned.Id));
        agentFromGet.ShouldBe(agentReturned with { Permissions = agentFromGet.Permissions });
        agentFromGet.Permissions.ShouldBe([Permission.Names.ManageTenant]);
    }

    [Test]
    public async Task agent_sent_without_permissions_starts_with_an_empty_grant()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var dto = new FakeAgentForCreationDto().Generate() with { Permissions = null };

        // Act
        var agentReturned = await testingServiceScope.SendAsync(new AddAgent.Command(dto));

        // Assert
        agentReturned.Permissions.ShouldBeEmpty();
    }

    [Test]
    public async Task tenant_admin_cannot_add_a_machine_agent()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var dto = new FakeAgentForCreationDto().Generate() with { Mode = "machine" };

        // Act
        var act = () => testingServiceScope.SendAsync(new AddAgent.Command(dto));

        // Assert
        (await act.ShouldThrowAsync<ForbiddenAccessException>()).Message
            .ShouldBe("Only a client that can manage all tenants can do this.");
        (await AgentCountAsync(dto.ClientId)).ShouldBe(0);
    }

    [Test]
    public async Task machine_agent_is_forbidden_before_the_tenant_check_while_the_tenant_is_deleted()
    {
        // Arrange
        var pause = PauseAtQuery.After("tenants.delete.load_agents");
        await using var services = pause.BuildServices();
        await new TestingServiceScope().StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUser(out var admin, u => u.Role = "Admin")
            .BuildAsync();
        var deleter = new TestingServiceScope(services);
        var underTest = new TestingServiceScope();
        deleter.SetUserWithDefaults(identifier: admin.Identifier);
        underTest.SetUserWithDefaults(identifier: admin.Identifier);
        var delete = deleter.SendAsync(new DeleteTenant.Command(tenant.Id));
        await pause.ReachedAsync();
        var dto = new FakeAgentForCreationDto().Generate() with { Mode = "machine" };

        // Act
        var add = underTest.SendAsync(new AddAgent.Command(dto));

        // Assert
        (await Should.ThrowAsync<ForbiddenAccessException>(add.WaitAsync(TimeSpan.FromSeconds(10)))).Message
            .ShouldBe("Only a client that can manage all tenants can do this.");
        pause.Release();
        await delete;
    }

    [Test]
    public async Task operator_binds_a_machine_agent_to_the_tenant_that_it_names()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _).AddTenant(out var tenant).BuildAsync();
        testingServiceScope.SetMachineClient(TestFixture.TrustedMachineClientId);
        var dto = new FakeAgentForCreationDto().Generate() with
        {
            Mode = "machine",
            TenantId = tenant.Id,
            Permissions = [Permission.Names.ManageTenant]
        };

        // Act
        var agentReturned = await testingServiceScope.SendAsync(new AddAgent.Command(dto));

        // Assert
        agentReturned.TenantId.ShouldBe(tenant.Id);
        agentReturned.Mode.ShouldBe("machine");
        agentReturned.Permissions.ShouldBe([Permission.Names.ManageTenant]);
    }

    [Test]
    public async Task operator_must_name_the_tenant_of_an_agent()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        testingServiceScope.SetMachineClient(TestFixture.TrustedMachineClientId);
        var dto = new FakeAgentForCreationDto().Generate() with { Mode = "machine" };

        // Act
        var act = () => testingServiceScope.SendAsync(new AddAgent.Command(dto));

        // Assert
        (await act.ShouldThrowAsync<ValidationException>()).Property.ShouldBe("tenantId");
    }

    [Test]
    public async Task tenant_admin_cannot_add_an_agent_to_another_tenant()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _).AddTenant(out var otherTenant).BuildAsync();
        var dto = new FakeAgentForCreationDto().Generate() with { TenantId = otherTenant.Id };

        // Act
        var act = () => testingServiceScope.SendAsync(new AddAgent.Command(dto));

        // Assert
        (await act.ShouldThrowAsync<ForbiddenAccessException>()).Message
            .ShouldBe("You can add an agent only to your own tenant.");
    }

    [Test]
    public async Task member_without_manage_agents_cannot_add_an_agent()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _).AddUser(out var member, u => u.Role = "User").BuildAsync();
        testingServiceScope.SetUserWithDefaults(identifier: member.Identifier);
        var dto = new FakeAgentForCreationDto().Generate();

        // Act
        var act = () => testingServiceScope.SendAsync(new AddAgent.Command(dto));

        // Assert
        (await act.ShouldThrowAsync<ForbiddenAccessException>()).Message
            .ShouldBe("You cannot manage agents. Ask a tenant admin for access.");
    }

    [Test]
    public async Task first_grant_above_what_the_caller_holds_is_denied()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var graph = testingServiceScope.StartGraph(out _).AddUser(out var member, u => u.Role = "User");
        member.AddPermission(Permission.ManageAgents());
        await graph.BuildAsync();
        testingServiceScope.SetUserWithDefaults(identifier: member.Identifier);
        var dto = new FakeAgentForCreationDto().Generate() with { Permissions = [Permission.Names.ManageTenant] };

        // Act
        var act = () => testingServiceScope.SendAsync(new AddAgent.Command(dto));

        // Assert
        (await act.ShouldThrowAsync<ForbiddenAccessException>()).Message
            .ShouldBe("You cannot manage the tenant. Ask a tenant admin for access.");
        (await AgentCountAsync(dto.ClientId)).ShouldBe(0);
    }

    [Test]
    public async Task second_agent_with_the_same_client_id_in_a_tenant_conflicts()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _).AddAgent(out var agent).BuildAsync();
        var dto = new FakeAgentForCreationDto().Generate() with { ClientId = agent.ClientId };

        // Act
        var act = () => testingServiceScope.SendAsync(new AddAgent.Command(dto));

        // Assert
        (await act.ShouldThrowAsync<ConflictException>()).Property.ShouldBe("clientId");
    }

    [Test]
    public async Task each_tenant_can_register_the_same_delegated_client()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _).AddTenant(out _).AddAgent(out var otherTenantAgent).BuildAsync();
        var dto = new FakeAgentForCreationDto().Generate() with { ClientId = otherTenantAgent.ClientId };

        // Act
        var agentReturned = await testingServiceScope.SendAsync(new AddAgent.Command(dto));

        // Assert
        agentReturned.TenantId.ShouldBe(TestFixture.DefaultTenant.Id);
        (await AgentCountAsync(dto.ClientId)).ShouldBe(2);
    }

    [Test]
    public async Task machine_client_bound_to_one_tenant_cannot_be_bound_to_another()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddTenant(out _).AddAgent(out var machineAgent, a => a.Mode = "machine")
            .BuildAsync();
        testingServiceScope.SetMachineClient(TestFixture.TrustedMachineClientId);
        var dto = new FakeAgentForCreationDto().Generate() with
        {
            Mode = "machine",
            ClientId = machineAgent.ClientId,
            TenantId = TestFixture.DefaultTenant.Id
        };

        // Act
        var act = () => testingServiceScope.SendAsync(new AddAgent.Command(dto));

        // Assert
        (await act.ShouldThrowAsync<ConflictException>()).Property.ShouldBe("clientId");
    }

    [Test]
    public async Task delegated_agent_cannot_take_the_client_of_a_machine_agent_of_another_tenant()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddTenant(out _).AddAgent(out var machineAgent, a => a.Mode = "machine")
            .BuildAsync();
        var dto = new FakeAgentForCreationDto().Generate() with { ClientId = machineAgent.ClientId, Mode = "delegated" };

        // Act
        var act = () => testingServiceScope.SendAsync(new AddAgent.Command(dto));

        // Assert
        var exception = await act.ShouldThrowAsync<ConflictException>();
        exception.Property.ShouldBe("clientId");
        exception.Message.ShouldBe("An agent with this client ID already exists.");
        (await AgentCountAsync(dto.ClientId)).ShouldBe(1);
    }

    [Test]
    public async Task operator_binds_a_machine_client_that_other_tenants_registered_as_delegated()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _).AddTenant(out _).AddAgent(out var delegatedAgent).BuildAsync();
        testingServiceScope.SetMachineClient(TestFixture.TrustedMachineClientId);
        var dto = new FakeAgentForCreationDto().Generate() with
        {
            Mode = "machine",
            ClientId = delegatedAgent.ClientId,
            TenantId = TestFixture.DefaultTenant.Id
        };

        // Act
        var agentReturned = await testingServiceScope.SendAsync(new AddAgent.Command(dto));

        // Assert
        agentReturned.Mode.ShouldBe("machine");
        (await AgentCountAsync(dto.ClientId)).ShouldBe(2);
    }

    [Test]
    [Arguments("delegated")]
    [Arguments("machine")]
    public async Task client_of_the_app_cannot_be_an_agent(string mode)
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        testingServiceScope.SetMachineClient(TestFixture.TrustedMachineClientId);
        var dto = new FakeAgentForCreationDto().Generate() with
        {
            Mode = mode,
            ClientId = TestFixture.FirstPartyClientId,
            TenantId = TestFixture.DefaultTenant.Id
        };

        // Act
        var act = () => testingServiceScope.SendAsync(new AddAgent.Command(dto));

        // Assert
        var exception = await act.ShouldThrowAsync<ValidationException>();
        exception.Property.ShouldBe("clientId");
        exception.Message.ShouldBe(
            "This is the client of the app. Its users act with their own permissions, so it cannot be an agent.");
        (await AgentCountAsync(dto.ClientId)).ShouldBe(0);
    }

    [Test]
    [Arguments(TestFixture.TrustedMachineClientId, "delegated")]
    [Arguments(TestFixture.TrustedMachineClientId, "machine")]
    [Arguments(TestFixture.ReadOnlyMachineClientId, "delegated")]
    [Arguments(TestFixture.ReadOnlyMachineClientId, "machine")]
    public async Task client_that_acts_for_all_tenants_cannot_be_an_agent(string clientId, string mode)
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        testingServiceScope.SetMachineClient(TestFixture.TrustedMachineClientId);
        var dto = new FakeAgentForCreationDto().Generate() with
        {
            Mode = mode,
            ClientId = clientId,
            TenantId = TestFixture.DefaultTenant.Id
        };

        // Act
        var act = () => testingServiceScope.SendAsync(new AddAgent.Command(dto));

        // Assert
        var exception = await act.ShouldThrowAsync<ValidationException>();
        exception.Property.ShouldBe("clientId");
        exception.Message.ShouldBe("This client acts for all tenants, so it cannot be an agent of one tenant.");
        (await AgentCountAsync(dto.ClientId)).ShouldBe(0);
    }

    [Test]
    public async Task unique_index_stops_a_second_live_machine_agent_for_a_client()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var clientId = new FakeAgentForCreationDto().Generate().ClientId;
        var graph = testingServiceScope.StartGraph(out _)
            .AddTenants(out _, 2)
            .AddAgents(out _, a => { a.Mode = "machine"; a.ClientId = clientId; });

        // Act
        var act = () => graph.BuildAsync();

        // Assert
        var exception = await act.ShouldThrowAsync<ConstraintConflictException>();
        exception.Constraint.ShouldBe("ix_agents_client_id");
        exception.Property.ShouldBe("clientId");
    }

    [Test]
    public async Task deleted_agent_frees_its_client_id()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _).AddAgent(out var agent).BuildAsync();
        await testingServiceScope.SendAsync(new DeleteAgent.Command(agent.Id));
        var dto = new FakeAgentForCreationDto().Generate() with { ClientId = agent.ClientId };

        // Act
        var agentReturned = await testingServiceScope.SendAsync(new AddAgent.Command(dto));

        // Assert
        agentReturned.Id.ShouldNotBe(agent.Id);
        agentReturned.ClientId.ShouldBe(agent.ClientId);
    }

    private static Task<int> AgentCountAsync(string clientId)
        => new TestingServiceScope().ExecuteDbContextAsync(db => db.Agents
            .IgnoreQueryFilters()
            .CountAsync(a => a.ClientId == clientId));
}
