namespace FullstackTemplate.IntegrationTests.FeatureTests.Agents;

using FullstackTemplate.Server.Domain.Agents.Features;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Shouldly;

/// <summary>A read needs read_agents. The tenant filter decides which agents a caller can get.</summary>
public class GetAgentQueryTests : TestBase
{
    [Test]
    public async Task member_with_only_read_agents_gets_an_agent_of_their_tenant()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddAgent(out var agent, a => a.Permissions = [Permission.Names.ManageTenant])
            .BuildAsync();
        await testingServiceScope.SignInAsMemberWithOnlyAsync(Permission.ReadAgents());

        // Act
        var agentReturned = await testingServiceScope.SendAsync(new GetAgent.Query(agent.Id));

        // Assert
        agentReturned.Id.ShouldBe(agent.Id);
        agentReturned.TenantId.ShouldBe(TestFixture.DefaultTenant.Id);
        agentReturned.Name.ShouldBe(agent.Name);
        agentReturned.ClientId.ShouldBe(agent.ClientId);
        agentReturned.Mode.ShouldBe("delegated");
        agentReturned.Permissions.ShouldBe([Permission.Names.ManageTenant]);
    }

    [Test]
    public async Task member_with_no_permission_cannot_get_an_agent()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _).AddAgent(out var agent).BuildAsync();
        await testingServiceScope.SignInAsMemberWithOnlyAsync();

        // Act
        var act = () => testingServiceScope.SendAsync(new GetAgent.Query(agent.Id));

        // Assert
        (await act.ShouldThrowAsync<ForbiddenAccessException>()).Message
            .ShouldBe("You cannot see the agents of the tenant. Ask a tenant admin for access.");
    }

    [Test]
    public async Task agent_of_another_tenant_is_not_found()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _).AddTenant(out _).AddAgent(out var otherTenantAgent).BuildAsync();

        // Act
        var act = () => testingServiceScope.SendAsync(new GetAgent.Query(otherTenantAgent.Id));

        // Assert
        await act.ShouldThrowAsync<NotFoundException>();
    }
}
