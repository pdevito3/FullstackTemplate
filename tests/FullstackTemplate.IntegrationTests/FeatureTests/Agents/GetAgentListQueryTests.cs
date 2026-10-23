namespace FullstackTemplate.IntegrationTests.FeatureTests.Agents;

using FullstackTemplate.Server.Domain.Agents.Dtos;
using FullstackTemplate.Server.Domain.Agents.Features;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Shouldly;

/// <summary>A read needs no permission. The tenant filter decides which agents a caller sees.</summary>
public class GetAgentListQueryTests : TestBase
{
    [Test]
    public async Task member_with_no_permission_sees_only_the_agents_of_their_tenant_by_name()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUser(out var member, u => u.Role = "User")
            .Under(tenant)
            .AddAgents(out var agents, a => a.Name = "Zeta sync", a => a.Name = "Alpha assistant")
            .BuildAsync();
        await testingServiceScope.StartGraph(out _).AddTenant(out _).AddAgent(out _).BuildAsync();
        testingServiceScope.SetUserWithDefaults(identifier: member.Identifier);

        // Act
        var list = await testingServiceScope.SendAsync(new GetAgentList.Query(AgentParametersDto.From(null, 500, null, null)));

        // Assert
        list.Select(a => a.Name).ShouldBe(["Alpha assistant", "Zeta sync"]);
        list.Select(a => a.Id).ShouldBe([agents[1].Id, agents[0].Id]);
        list.TotalCount.ShouldBe(2);
    }

    [Test]
    public async Task unknown_client_sees_no_agent()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _).AddAgent(out _).BuildAsync();
        testingServiceScope.SetMachineClient("unregistered-client");

        // Act
        var list = await testingServiceScope.SendAsync(new GetAgentList.Query(AgentParametersDto.From(null, 500, null, null)));

        // Assert
        list.ShouldBeEmpty();
        list.TotalCount.ShouldBe(0);
    }
}
