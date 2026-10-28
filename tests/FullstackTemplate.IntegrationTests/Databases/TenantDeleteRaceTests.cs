namespace FullstackTemplate.IntegrationTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.Agents.Features;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.Server.Resources;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Fakes.Agent;
using FullstackTemplate.SharedTestHelpers.Utilities;
using Microsoft.EntityFrameworkCore;
using Shouldly;

/// <summary>
/// A tenant delete deletes the agents of the tenant. An agent add at the same time must not leave a live agent in the
/// deleted tenant, so each takes the lock of the tenant, and the second waits for the first. See
/// <c>docs/backend/concurrency.md</c>, section "Lock".
/// </summary>
public class TenantDeleteRaceTests : TestBase
{
    [Test]
    public async Task agent_add_during_a_tenant_delete_waits_and_then_finds_no_tenant()
    {
        // Arrange
        var pause = PauseAtQuery.After("tenants.delete.load_agents");
        await using var services = pause.BuildServices();
        await new TestingServiceScope().StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUser(out var admin, u => u.Role = "Admin")
            .BuildAsync();
        var underTest = new TestingServiceScope(services);
        var rival = new TestingServiceScope();
        underTest.SetUserWithDefaults(identifier: admin.Identifier);
        rival.SetUserWithDefaults(identifier: admin.Identifier);

        // Act
        var delete = underTest.SendAsync(new DeleteTenant.Command(tenant.Id));
        await pause.ReachedAsync();
        var add = rival.SendAsync(new AddAgent.Command(new FakeAgentForCreationDto().Generate()));
        await pause.WaitUntilBlockedAsync(add);
        pause.Release();

        // Assert
        await delete;
        (await Should.ThrowAsync<ValidationException>(add)).Message.ShouldBe("The tenant does not exist.");
        (await TenantIsDeletedAsync(tenant.Id)).ShouldBeTrue();
        (await LiveAgentCountAsync(tenant.Id)).ShouldBe(0);
    }

    [Test]
    public async Task tenant_delete_during_an_agent_add_waits_and_then_deletes_the_new_agent()
    {
        // Arrange
        var pause = PauseAtQuery.After("agents.add.client_id_in_use");
        await using var services = pause.BuildServices();
        await new TestingServiceScope().StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUser(out var admin, u => u.Role = "Admin")
            .BuildAsync();
        var underTest = new TestingServiceScope(services);
        var rival = new TestingServiceScope();
        underTest.SetUserWithDefaults(identifier: admin.Identifier);
        rival.SetUserWithDefaults(identifier: admin.Identifier);

        // Act
        var add = underTest.SendAsync(new AddAgent.Command(new FakeAgentForCreationDto().Generate()));
        await pause.ReachedAsync();
        var delete = rival.SendAsync(new DeleteTenant.Command(tenant.Id));
        await pause.WaitUntilBlockedAsync(delete);
        pause.Release();

        // Assert
        var agent = await add;
        await delete;
        (await AgentIsDeletedAsync(agent.Id)).ShouldBeTrue();
        (await TenantIsDeletedAsync(tenant.Id)).ShouldBeTrue();
        (await LiveAgentCountAsync(tenant.Id)).ShouldBe(0);
    }

    private static Task<bool> TenantIsDeletedAsync(string tenantId)
        => new TestingServiceScope().ExecuteDbContextAsync(db =>
            db.Tenants.IgnoreQueryFilters().Where(t => t.Id == tenantId).Select(t => t.IsDeleted).SingleAsync());

    private static Task<bool> AgentIsDeletedAsync(string agentId)
        => new TestingServiceScope().ExecuteDbContextAsync(db =>
            db.Agents.IgnoreQueryFilters().Where(a => a.Id == agentId).Select(a => a.IsDeleted).SingleAsync());

    private static Task<int> LiveAgentCountAsync(string tenantId)
        => new TestingServiceScope().ExecuteDbContextAsync(db =>
            db.Agents.IgnoreQueryFilters([QueryFilterNames.Tenant]).CountAsync(a => a.TenantId == tenantId));
}
