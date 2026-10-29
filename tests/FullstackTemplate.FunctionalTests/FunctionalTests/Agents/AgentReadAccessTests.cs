namespace FullstackTemplate.FunctionalTests.FunctionalTests.Agents;

using System.Net;
using System.Net.Http.Json;
using FullstackTemplate.Server.Domain.Agents;
using FullstackTemplate.Server.Domain.Agents.Dtos;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using TestUtilities;

/// <summary>
/// A user with the read_agents permission can read the agents of the tenant. A command that returns an agent needs it
/// too, except AddAgent, whose response has only the values of the request and the new ID.
/// </summary>
public class AgentReadAccessTests : TestBase
{
    private const string ReadAgentsDenial = "You cannot see the agents of the tenant. Ask a tenant admin for access.";

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

    [Test]
    public async Task member_with_only_manage_agents_adds_an_agent_but_cannot_read_it_after()
    {
        // Arrange
        var (member, _) = await AddMemberAndAgentAsync(Permission.ManageAgents());
        SignInAs(member);
        var dto = new AgentForCreationDto { Name = "Report bot", ClientId = "report-bot", Mode = "delegated" };

        // Act
        var addResult = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Agents.Create, dto);
        var added = await addResult.Content.ReadFromJsonAsync<AgentDto>();
        var getResult = await FactoryClient.GetRequestAsync(ApiRoutes.Agents.GetRecord(added!.Id));

        // Assert
        addResult.StatusCode.ShouldBe(HttpStatusCode.Created);
        added.TenantId.ShouldBe(member.TenantId);
        added.Name.ShouldBe(dto.Name);
        added.ClientId.ShouldBe(dto.ClientId);
        added.Mode.ShouldBe(dto.Mode);
        added.Permissions.ShouldBeEmpty();
        await ShouldBeForbiddenAsync(getResult, ReadAgentsDenial);
    }

    // A command returns the record of the agent, so manage_agents alone must not show it
    [Test]
    public async Task member_with_only_manage_agents_cannot_add_a_permission_to_an_agent()
    {
        // Arrange
        var (member, agent) = await AddMemberAndAgentAsync([], Permission.ManageAgents(), Permission.ManageTenant());
        SignInAs(member);

        // Act
        var result = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Agents.AddPermission(agent.Id),
            new AgentPermissionDto { Permission = Permission.Names.ManageTenant });

        // Assert
        await ShouldBeForbiddenAsync(result, ReadAgentsDenial);
        (await GrantOfAsync(agent.Id)).ShouldBeEmpty();
    }

    [Test]
    public async Task member_with_only_manage_agents_cannot_remove_a_permission_from_an_agent()
    {
        // Arrange
        var (member, agent) = await AddMemberAndAgentAsync([Permission.Names.ManageTenant], Permission.ManageAgents());
        SignInAs(member);

        // Act
        var result = await FactoryClient.DeleteRequestAsync(
            ApiRoutes.Agents.RemovePermission(agent.Id, Permission.Names.ManageTenant));

        // Assert
        await ShouldBeForbiddenAsync(result, ReadAgentsDenial);
        (await GrantOfAsync(agent.Id)).ShouldBe([Permission.Names.ManageTenant]);
    }

    private static Task<List<string>> GrantOfAsync(string agentId)
        => ExecuteDbContextAsync(db => db.AgentPermissions
            .Where(p => p.AgentId == agentId)
            .Select(p => p.Permission.Value)
            .ToListAsync());

    private void SignInAs(User user)
        => FactoryClient.SignInAsAppUser(user.Identifier);

    private static async Task ShouldBeForbiddenAsync(HttpResponseMessage result, string detail)
    {
        result.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var problem = (await result.Content.ReadFromJsonAsync<ProblemDetails>()).ShouldNotBeNull();
        problem.Status.ShouldBe(StatusCodes.Status403Forbidden);
        problem.Detail.ShouldBe(detail);
    }

    private static Task<(User Member, Agent Agent)> AddMemberAndAgentAsync(params Permission[] permissions)
        => AddMemberAndAgentAsync([], permissions);

    private static async Task<(User Member, Agent Agent)> AddMemberAndAgentAsync(
        IReadOnlyList<string> grant, params Permission[] permissions)
    {
        TestGraph.Start(out _)
            .AddTenant(out var tenant)
            .AddUser(out var member, u => u.Role = "User")
            .Under(tenant)
            .AddAgent(out var agent, a => a.Permissions = grant);
        member.AddPermissions(permissions);
        await InsertAsync(tenant);
        await InsertAsync(member);
        await InsertAsync(agent);
        return (member, agent);
    }
}
