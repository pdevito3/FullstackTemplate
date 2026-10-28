namespace FullstackTemplate.IntegrationTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.Agents.Features;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Resources;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Fakes.Agent;
using FullstackTemplate.SharedTestHelpers.Utilities;
using Microsoft.EntityFrameworkCore;
using Shouldly;

/// <summary>
/// A tenant delete deletes the agents of the tenant. An agent add at the same time must not leave a live agent in the
/// deleted tenant. See <c>docs/backend/concurrency.md</c>, section "Lock".
/// </summary>
public class TenantDeleteRaceTests : TestBase
{
    [Test]
    public async Task agent_add_after_the_agent_load_of_a_tenant_delete_leaves_no_live_agent_in_the_deleted_tenant()
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
        await rival.SendAsync(new AddAgent.Command(new FakeAgentForCreationDto().Generate()));
        pause.Release();
        await delete;

        // Assert
        (await TenantIsDeletedAsync(tenant.Id)).ShouldBeTrue();
        (await LiveAgentCountAsync(tenant.Id)).ShouldBe(0);
    }

    [Test]
    public async Task tenant_delete_after_the_checks_of_an_agent_add_leaves_no_live_agent_in_the_deleted_tenant()
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
        await rival.SendAsync(new DeleteTenant.Command(tenant.Id));
        pause.Release();
        await add;

        // Assert
        (await TenantIsDeletedAsync(tenant.Id)).ShouldBeTrue();
        (await LiveAgentCountAsync(tenant.Id)).ShouldBe(0);
    }

    private static Task<bool> TenantIsDeletedAsync(string tenantId)
        => new TestingServiceScope().ExecuteDbContextAsync(db =>
            db.Tenants.IgnoreQueryFilters().Where(t => t.Id == tenantId).Select(t => t.IsDeleted).SingleAsync());

    private static Task<int> LiveAgentCountAsync(string tenantId)
        => new TestingServiceScope().ExecuteDbContextAsync(db =>
            db.Agents.IgnoreQueryFilters([QueryFilterNames.Tenant]).CountAsync(a => a.TenantId == tenantId));
}
