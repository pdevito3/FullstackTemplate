namespace FullstackTemplate.FunctionalTests.FunctionalTests.Agents;

using System.Net;
using System.Net.Http.Json;
using FullstackTemplate.Server.Domain.Agents;
using FullstackTemplate.Server.Domain.Agents.Dtos;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using TestUtilities;

/// <summary>A user with the read_agents permission can read the agents of the tenant.</summary>
public class AgentReadAccessTests : TestBase
{
    private const string ReadAgentsDenial = "You need the read_agents permission to do this.";

    [Test]
    public async Task member_without_read_agents_cannot_list_the_agents()
    {
        // Arrange
        var (member, _) = await AddMemberAndAgentAsync();
        SignInAs(member);

        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Agents.GetList);

        // Assert
        await ShouldBeForbiddenAsync(result, ReadAgentsDenial);
    }

    [Test]
    public async Task member_without_read_agents_cannot_get_an_agent()
    {
        // Arrange
        var (member, agent) = await AddMemberAndAgentAsync();
        SignInAs(member);

        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Agents.GetRecord(agent.Id));

        // Assert
        await ShouldBeForbiddenAsync(result, ReadAgentsDenial);
    }

    [Test]
    public async Task member_granted_read_agents_can_list_the_agents()
    {
        // Arrange
        var (member, agent) = await AddMemberAndAgentAsync(Permission.ReadAgents());
        SignInAs(member);

        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Agents.GetList);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var agents = (await result.Content.ReadFromJsonAsync<List<AgentDto>>()).ShouldNotBeNull();
        agents.ShouldHaveSingleItem().Id.ShouldBe(agent.Id);
    }

    [Test]
    public async Task member_granted_read_agents_can_get_an_agent()
    {
        // Arrange
        var (member, agent) = await AddMemberAndAgentAsync(Permission.ReadAgents());
        SignInAs(member);

        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Agents.GetRecord(agent.Id));

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await result.Content.ReadFromJsonAsync<AgentDto>()).ShouldNotBeNull().Id.ShouldBe(agent.Id);
    }

    private void SignInAs(User user)
        => FactoryClient.SignInAsAppUser(user.Identifier);

    private static async Task ShouldBeForbiddenAsync(HttpResponseMessage result, string detail)
    {
        result.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var problem = (await result.Content.ReadFromJsonAsync<ProblemDetails>()).ShouldNotBeNull();
        problem.Status.ShouldBe(StatusCodes.Status403Forbidden);
        problem.Detail.ShouldBe(detail);
    }

    private static async Task<(User Member, Agent Agent)> AddMemberAndAgentAsync(params Permission[] permissions)
    {
        TestGraph.Start(out _)
            .AddTenant(out var tenant)
            .AddUser(out var member, u => u.Role = "User")
            .Under(tenant)
            .AddAgent(out var agent);
        member.AddPermissions(permissions);
        await InsertAsync(tenant);
        await InsertAsync(member);
        await InsertAsync(agent);
        return (member, agent);
    }
}
