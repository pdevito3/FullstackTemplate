namespace FullstackTemplate.IntegrationTests.FeatureTests.Agents;

using FullstackTemplate.Server.Domain.Agents.Features;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Microsoft.EntityFrameworkCore;
using Shouldly;

/// <summary>
/// A revoke needs only manage_agents and read_agents, so a tenant admin can take a permission away from an agent that holds more than
/// the admin.
/// </summary>
public class RemoveAgentPermissionCommandTests : TestBase
{
    [Test]
    public async Task holder_of_manage_agents_and_the_permission_revokes_it()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddAgent(out var agent, a => a.Permissions = [Permission.Names.ManageTenant, Permission.Names.ManageUsers])
            .BuildAsync();
        await testingServiceScope.SignInAsMemberWithOnlyAsync(Permission.ManageAgents(), Permission.ReadAgents(), Permission.ManageTenant());

        // Act
        var agentReturned = await testingServiceScope.SendAsync(
            new RemoveAgentPermission.Command(agent.Id, Permission.Names.ManageTenant));

        // Assert
        agentReturned.Permissions.ShouldBe([Permission.Names.ManageUsers]);
        (await LiveGrantOfAsync(agent.Id)).ShouldBe([Permission.Names.ManageUsers]);
    }

    [Test]
    public async Task revoke_of_a_permission_that_is_not_granted_changes_nothing()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddAgent(out var agent, a => a.Permissions = [Permission.Names.ManageUsers])
            .BuildAsync();

        // Act
        var agentReturned = await testingServiceScope.SendAsync(
            new RemoveAgentPermission.Command(agent.Id, Permission.Names.ManageTenant));

        // Assert
        agentReturned.Permissions.ShouldBe([Permission.Names.ManageUsers]);
    }

    [Test]
    public async Task holder_of_only_manage_and_read_agents_revokes_a_permission_that_they_lack()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddAgent(out var agent, a => a.Permissions = [Permission.Names.ManageTenant, Permission.Names.ManageUsers])
            .BuildAsync();
        await testingServiceScope.SignInAsMemberWithOnlyAsync(Permission.ManageAgents(), Permission.ReadAgents());

        // Act
        var agentReturned = await testingServiceScope.SendAsync(
            new RemoveAgentPermission.Command(agent.Id, Permission.Names.ManageUsers));

        // Assert
        agentReturned.Permissions.ShouldBe([Permission.Names.ManageTenant]);
        (await LiveGrantOfAsync(agent.Id)).ShouldBe([Permission.Names.ManageTenant]);
    }

    [Test]
    public async Task member_without_manage_agents_cannot_revoke_a_permission()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddAgent(out var agent, a => a.Permissions = [Permission.Names.ManageTenant])
            .BuildAsync();
        await testingServiceScope.SignInAsMemberWithOnlyAsync(Permission.ManageTenant());

        // Act
        var act = () => testingServiceScope.SendAsync(
            new RemoveAgentPermission.Command(agent.Id, Permission.Names.ManageTenant));

        // Assert
        (await act.ShouldThrowAsync<ForbiddenAccessException>()).Message
            .ShouldBe("You need the manage_agents permission to do this.");
        (await LiveGrantOfAsync(agent.Id)).ShouldBe([Permission.Names.ManageTenant]);
    }

    private static Task<List<string>> LiveGrantOfAsync(string agentId)
        => new TestingServiceScope().ExecuteDbContextAsync(db => db.AgentPermissions
            .Where(p => p.AgentId == agentId)
            .Select(p => p.Permission.Value)
            .ToListAsync());

    [Test]
    public async Task response_has_the_permissions_that_get_agent_returns()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddAgent(out var agent, a => a.Permissions = [Permission.Names.ReadUsers, Permission.Names.ReadAgents])
            .BuildAsync();

        // Act
        var agentReturned = await testingServiceScope.SendAsync(
            new RemoveAgentPermission.Command(agent.Id, Permission.Names.ReadUsers));

        // Assert
        var agentFromGet = await new TestingServiceScope().SendAsync(new GetAgent.Query(agent.Id));
        agentFromGet.Permissions.ShouldBe([Permission.Names.ReadAgents]);
        agentReturned.Permissions.ShouldBe(agentFromGet.Permissions, ignoreOrder: true);
    }
}
