namespace FullstackTemplate.IntegrationTests.FeatureTests.Agents;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.Agents.Features;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Fakes.Agent;
using Microsoft.EntityFrameworkCore;
using Shouldly;

/// <summary>
/// A tenant admin registers a delegated agent in their own tenant. Only a caller that manages all tenants binds a
/// machine agent, and a client ID has at most one live agent in a tenant and one live machine agent in all tenants.
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
            .ShouldBe("You need the manage_agents permission to do this.");
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
            .ShouldBe("You need the manage_tenant permission to do this.");
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
