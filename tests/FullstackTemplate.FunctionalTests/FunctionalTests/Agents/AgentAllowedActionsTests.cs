namespace FullstackTemplate.FunctionalTests.FunctionalTests.Agents;

using System.Net;
using System.Text.Json;
using FullstackTemplate.Server.Domain.Agents;
using FullstackTemplate.Server.Domain.Agents.Dtos;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Shouldly;
using TestUtilities;

public class AgentAllowedActionsTests : TestBase
{
    [Test]
    public async Task manager_who_can_read_agents_gets_each_action_for_an_agent()
    {
        // Arrange
        var (manager, agent) = await AddMemberAndAgentAsync(Permission.ManageAgents(), Permission.ReadAgents());
        SignInAs(manager);

        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Agents.GetRecord(agent.Id));

        // Assert
        (await AllowedActionsOfAsync(result)).ShouldBe(["AddPermission", "RemovePermission", "Delete"]);
    }

    [Test]
    public async Task reader_with_no_other_permission_gets_no_action_for_an_agent()
    {
        // Arrange
        var (reader, agent) = await AddMemberAndAgentAsync(Permission.ReadAgents());
        SignInAs(reader);

        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Agents.GetRecord(agent.Id));

        // Assert
        (await AllowedActionsOfAsync(result)).ShouldBeEmpty();
    }

    [Test]
    public async Task each_row_of_the_list_has_the_actions_of_the_caller()
    {
        // Arrange
        var (manager, agent) = await AddMemberAndAgentAsync(Permission.ManageAgents(), Permission.ReadAgents());
        SignInAs(manager);

        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Agents.GetList);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await result.Content.ReadAsStringAsync());
        var row = body.RootElement.EnumerateArray().ShouldHaveSingleItem();
        row.GetProperty("id").GetString().ShouldBe(agent.Id);
        NamesIn(row).ShouldBe(["AddPermission", "RemovePermission", "Delete"]);
    }

    [Test]
    public async Task add_permission_response_has_the_actions_of_the_caller()
    {
        // Arrange
        var (manager, agent) = await AddMemberAndAgentAsync(Permission.ManageAgents(), Permission.ReadAgents());
        SignInAs(manager);

        // Act
        var result = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Agents.AddPermission(agent.Id),
            new AgentPermissionDto { Permission = Permission.Names.ReadAgents });

        // Assert
        (await AllowedActionsOfAsync(result)).ShouldBe(["AddPermission", "RemovePermission", "Delete"]);
    }

    [Test]
    public async Task remove_permission_response_has_the_actions_of_the_caller()
    {
        // Arrange
        var (manager, agent) = await AddMemberAndAgentAsync(Permission.ManageAgents(), Permission.ReadAgents());
        SignInAs(manager);

        // Act
        var result = await FactoryClient.DeleteRequestAsync(
            ApiRoutes.Agents.RemovePermission(agent.Id, Permission.Names.ReadAgents));

        // Assert
        (await AllowedActionsOfAsync(result)).ShouldBe(["AddPermission", "RemovePermission", "Delete"]);
    }

    [Test]
    public async Task manager_who_cannot_read_agents_gets_only_delete_for_the_agent_they_add()
    {
        // Arrange
        var (manager, _) = await AddMemberAndAgentAsync(Permission.ManageAgents());
        SignInAs(manager);
        var dto = new AgentForCreationDto { Name = "Report bot", ClientId = "report-bot", Mode = "delegated" };

        // Act
        var result = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Agents.Create, dto);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var body = JsonDocument.Parse(await result.Content.ReadAsStringAsync());
        NamesIn(body.RootElement).ShouldBe(["Delete"]);
    }

    private static async Task<string[]> AllowedActionsOfAsync(HttpResponseMessage result)
    {
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await result.Content.ReadAsStringAsync());
        return NamesIn(body.RootElement);
    }

    private static string[] NamesIn(JsonElement agent)
    {
        agent.TryGetProperty("allowedActions", out var allowedActions)
            .ShouldBeTrue("The agent record has no allowedActions.");
        return allowedActions.EnumerateArray().Select(a => a.GetString()!).ToArray();
    }

    private void SignInAs(User user)
        => FactoryClient.SignInAsAppUser(user.Identifier);

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
