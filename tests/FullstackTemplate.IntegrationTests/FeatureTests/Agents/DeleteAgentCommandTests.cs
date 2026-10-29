namespace FullstackTemplate.IntegrationTests.FeatureTests.Agents;

using FullstackTemplate.Server.Domain.Agents.Features;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Microsoft.EntityFrameworkCore;
using Shouldly;

/// <summary>
/// A delete needs only manage_agents, so a tenant admin can remove an agent that holds more than the admin.
/// </summary>
public class DeleteAgentCommandTests : TestBase
{
    [Test]
    public async Task admin_deletes_an_agent_and_its_grant()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddAgent(out var agent, a => a.Permissions = [Permission.Names.ManageTenant])
            .BuildAsync();

        // Act
        await testingServiceScope.SendAsync(new DeleteAgent.Command(agent.Id));

        // Assert
        var get = () => new TestingServiceScope().SendAsync(new GetAgent.Query(agent.Id));
        await get.ShouldThrowAsync<NotFoundException>();
        var isDeleted = await IsDeletedFlagsOfAsync(agent.Id);
        isDeleted.Agent.ShouldBeTrue();
        isDeleted.Grant.ShouldBe([true]);
    }

    [Test]
    public async Task member_without_manage_agents_cannot_delete_an_agent()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _).AddAgent(out var agent).BuildAsync();
        await testingServiceScope.SignInAsMemberWithOnlyAsync();

        // Act
        var act = () => testingServiceScope.SendAsync(new DeleteAgent.Command(agent.Id));

        // Assert
        (await act.ShouldThrowAsync<ForbiddenAccessException>()).Message
            .ShouldBe("You cannot manage agents. Ask a tenant admin for access.");
        (await IsDeletedFlagsOfAsync(agent.Id)).Agent.ShouldBeFalse();
    }

    [Test]
    public async Task holder_of_only_manage_agents_deletes_an_agent_that_holds_more()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddAgent(out var agent, a => a.Permissions = [Permission.Names.ManageTenant, Permission.Names.ManageUsers])
            .BuildAsync();
        await testingServiceScope.SignInAsMemberWithOnlyAsync(Permission.ManageAgents());

        // Act
        await testingServiceScope.SendAsync(new DeleteAgent.Command(agent.Id));

        // Assert
        var isDeleted = await IsDeletedFlagsOfAsync(agent.Id);
        isDeleted.Agent.ShouldBeTrue();
        isDeleted.Grant.ShouldBe([true, true]);
    }

    [Test]
    public async Task agent_of_another_tenant_is_not_found()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _).AddTenant(out _).AddAgent(out var otherTenantAgent).BuildAsync();

        // Act
        var act = () => testingServiceScope.SendAsync(new DeleteAgent.Command(otherTenantAgent.Id));

        // Assert
        await act.ShouldThrowAsync<NotFoundException>();
        (await IsDeletedFlagsOfAsync(otherTenantAgent.Id)).Agent.ShouldBeFalse();
    }

    private static Task<(bool Agent, List<bool> Grant)> IsDeletedFlagsOfAsync(string agentId)
        => new TestingServiceScope().ExecuteDbContextAsync(async db => (
            await db.Agents.IgnoreQueryFilters().Where(a => a.Id == agentId).Select(a => a.IsDeleted).SingleAsync(),
            await db.AgentPermissions.IgnoreQueryFilters().Where(p => p.AgentId == agentId).Select(p => p.IsDeleted)
                .ToListAsync()));
}
