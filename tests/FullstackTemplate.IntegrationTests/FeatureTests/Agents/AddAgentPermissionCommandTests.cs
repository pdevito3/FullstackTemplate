namespace FullstackTemplate.IntegrationTests.FeatureTests.Agents;

using FullstackTemplate.Server.Domain.Agents.Features;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.Server.Resources;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Microsoft.EntityFrameworkCore;
using Shouldly;

/// <summary>A grant needs manage_agents, and the caller must hold the permission that it grants.</summary>
public class AddAgentPermissionCommandTests : TestBase
{
    [Test]
    public async Task holder_of_manage_agents_and_the_permission_grants_it()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _).AddAgent(out var agent).BuildAsync();
        await testingServiceScope.SignInAsMemberWithOnlyAsync(Permission.ManageAgents(), Permission.ManageTenant());

        // Act
        var agentReturned = await testingServiceScope.SendAsync(
            new AddAgentPermission.Command(agent.Id, Permission.Names.ManageTenant));

        // Assert
        agentReturned.Permissions.ShouldBe([Permission.Names.ManageTenant]);
        (await GrantOfAsync(agent.Id)).ShouldBe([Permission.Names.ManageTenant]);
    }

    [Test]
    public async Task second_grant_of_a_permission_keeps_one_row()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddAgent(out var agent, a => a.Permissions = [Permission.Names.ManageTenant])
            .BuildAsync();

        // Act
        await testingServiceScope.SendAsync(new AddAgentPermission.Command(agent.Id, Permission.Names.ManageTenant));

        // Assert
        (await GrantOfAsync(agent.Id)).ShouldBe([Permission.Names.ManageTenant]);
    }

    [Test]
    public async Task holder_of_only_manage_agents_cannot_grant_a_permission_that_they_lack()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _).AddAgent(out var agent).BuildAsync();
        await testingServiceScope.SignInAsMemberWithOnlyAsync(Permission.ManageAgents());

        // Act
        var act = () => testingServiceScope.SendAsync(
            new AddAgentPermission.Command(agent.Id, Permission.Names.ManageTenant));

        // Assert
        (await act.ShouldThrowAsync<ForbiddenAccessException>()).Message
            .ShouldBe("You need the manage_tenant permission to do this.");
        (await GrantOfAsync(agent.Id)).ShouldBeEmpty();
    }

    [Test]
    public async Task member_without_manage_agents_cannot_grant_a_permission()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _).AddAgent(out var agent).BuildAsync();
        await testingServiceScope.SignInAsMemberWithOnlyAsync(Permission.ManageTenant());

        // Act
        var act = () => testingServiceScope.SendAsync(
            new AddAgentPermission.Command(agent.Id, Permission.Names.ManageTenant));

        // Assert
        (await act.ShouldThrowAsync<ForbiddenAccessException>()).Message
            .ShouldBe("You need the manage_agents permission to do this.");
        (await GrantOfAsync(agent.Id)).ShouldBeEmpty();
    }

    [Test]
    public async Task unknown_permission_is_a_validation_error()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _).AddAgent(out var agent).BuildAsync();

        // Act
        var act = () => testingServiceScope.SendAsync(new AddAgentPermission.Command(agent.Id, "manage_everything"));

        // Assert
        (await act.ShouldThrowAsync<ValidationException>()).Property.ShouldBe("permission");
    }

    [Test]
    public async Task agent_of_another_tenant_is_not_found()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _).AddTenant(out _).AddAgent(out var otherTenantAgent).BuildAsync();

        // Act
        var act = () => testingServiceScope.SendAsync(
            new AddAgentPermission.Command(otherTenantAgent.Id, Permission.Names.ManageTenant));

        // Assert
        await act.ShouldThrowAsync<NotFoundException>();
        (await GrantOfAsync(otherTenantAgent.Id)).ShouldBeEmpty();
    }

    private static Task<List<string>> GrantOfAsync(string agentId)
        => new TestingServiceScope().ExecuteDbContextAsync(db => db.AgentPermissions
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .Where(p => p.AgentId == agentId)
            .Select(p => p.Permission.Value)
            .ToListAsync());
}
