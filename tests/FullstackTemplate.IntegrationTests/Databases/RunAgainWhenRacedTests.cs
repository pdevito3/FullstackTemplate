namespace FullstackTemplate.IntegrationTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.Agents.Features;
using FullstackTemplate.Server.Domain.Tenants.Dtos;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.Features;
using FullstackTemplate.Server.Resources;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Utilities;
using Microsoft.EntityFrameworkCore;
using Shouldly;

/// <summary>
/// Each command here sends no version. A rival commits a change to the same aggregate after the load of the command,
/// so the first save of the command loses on the version of the owner row. The command then runs again on the new
/// rows and saves. See <c>docs/backend/concurrency.md</c>.
/// </summary>
public class RunAgainWhenRacedTests : TestBase
{
    [Test]
    public async Task permission_add_that_a_rival_add_beat_after_the_load_runs_again_and_both_save()
    {
        // Arrange
        var pause = PauseAtQuery.After("users.add_permission.load");
        await using var services = pause.BuildServices();
        await new TestingServiceScope().StartGraph(out _)
            .AddUser(out var user, u => u.Role = "User")
            .BuildAsync();

        // Act
        var add = new TestingServiceScope(services).SendAsync(
            new AddUserPermission.Command(user.Id, Permission.Names.ManageUsers));
        await pause.ReachedAsync();
        await new TestingServiceScope().SendAsync(new AddUserPermission.Command(user.Id, Permission.Names.ReadUsers));
        pause.Release();

        // Assert
        var added = await add;
        added.Permissions.ShouldBe([Permission.Names.ManageUsers, Permission.Names.ReadUsers], ignoreOrder: true);
        (await UserPermissionsAsync(user.Id))
            .ShouldBe([Permission.Names.ManageUsers, Permission.Names.ReadUsers], ignoreOrder: true);
    }

    [Test]
    public async Task permission_remove_that_a_rival_add_beat_after_the_load_runs_again_and_both_save()
    {
        // Arrange
        var pause = PauseAtQuery.After("users.remove_permission.load");
        await using var services = pause.BuildServices();
        await new TestingServiceScope().StartGraph(out _)
            .AddUser(out var user, u => u.Role = "User")
            .BuildAsync();
        await new TestingServiceScope().SendAsync(new AddUserPermission.Command(user.Id, Permission.Names.ManageUsers));

        // Act
        var remove = new TestingServiceScope(services).SendAsync(
            new RemoveUserPermission.Command(user.Id, Permission.Names.ManageUsers));
        await pause.ReachedAsync();
        await new TestingServiceScope().SendAsync(new AddUserPermission.Command(user.Id, Permission.Names.ReadUsers));
        pause.Release();

        // Assert
        (await remove).Permissions.ShouldBe([Permission.Names.ReadUsers]);
        (await UserPermissionsAsync(user.Id)).ShouldBe([Permission.Names.ReadUsers]);
    }

    [Test]
    public async Task role_change_that_a_rival_permission_add_beat_after_the_load_runs_again_and_saves()
    {
        // Arrange
        var pause = PauseAtQuery.After("users.update_role.load");
        await using var services = pause.BuildServices();
        await new TestingServiceScope().StartGraph(out _)
            .AddUser(out var user, u => u.Role = "User")
            .BuildAsync();

        // Act
        var change = new TestingServiceScope(services).SendAsync(new UpdateUserRole.Command(user.Id, "Admin"));
        await pause.ReachedAsync();
        await new TestingServiceScope().SendAsync(new AddUserPermission.Command(user.Id, Permission.Names.ReadUsers));
        pause.Release();

        // Assert
        (await change).Role.ShouldBe("Admin");
        var saved = await new TestingServiceScope().ExecuteDbContextAsync(db =>
            db.Users.IgnoreQueryFilters([QueryFilterNames.Tenant]).Include(u => u.UserPermissions).GetById(user.Id));
        saved.Role.Value.ShouldBe("Admin");
        saved.UserPermissions.Select(p => p.Permission.Value)
            .ShouldBe(UserRole.Of("Admin").GetDefaultPermissions().Select(p => p.Value), ignoreOrder: true);
    }

    [Test]
    public async Task user_delete_that_a_rival_permission_add_beat_after_the_load_runs_again_and_deletes()
    {
        // Arrange
        var pause = PauseAtQuery.After("users.delete.load");
        await using var services = pause.BuildServices();
        await new TestingServiceScope().StartGraph(out _)
            .AddUser(out var user, u => u.Role = "User")
            .BuildAsync();

        // Act
        var delete = new TestingServiceScope(services).SendAsync(new DeleteUser.Command(user.Id));
        await pause.ReachedAsync();
        await new TestingServiceScope().SendAsync(new AddUserPermission.Command(user.Id, Permission.Names.ReadUsers));
        pause.Release();

        // Assert
        await delete;
        (await new TestingServiceScope().ExecuteDbContextAsync(db => db.Users.IgnoreQueryFilters([QueryFilterNames.Tenant]).AnyAsync(u => u.Id == user.Id)))
            .ShouldBeFalse();
    }

    [Test]
    public async Task agent_permission_add_that_a_rival_add_beat_after_the_load_runs_again_and_both_save()
    {
        // Arrange
        var pause = PauseAtQuery.After("agents.add_permission.load");
        await using var services = pause.BuildServices();
        await new TestingServiceScope().StartGraph(out _).AddAgent(out var agent).BuildAsync();

        // Act
        var add = new TestingServiceScope(services).SendAsync(
            new AddAgentPermission.Command(agent.Id, Permission.Names.ManageTenant));
        await pause.ReachedAsync();
        await new TestingServiceScope().SendAsync(new AddAgentPermission.Command(agent.Id, Permission.Names.ReadUsers));
        pause.Release();

        // Assert
        (await add).Permissions.ShouldBe([Permission.Names.ManageTenant, Permission.Names.ReadUsers], ignoreOrder: true);
        (await AgentPermissionsAsync(agent.Id))
            .ShouldBe([Permission.Names.ManageTenant, Permission.Names.ReadUsers], ignoreOrder: true);
    }

    [Test]
    public async Task agent_permission_remove_that_a_rival_add_beat_after_the_load_runs_again_and_both_save()
    {
        // Arrange
        var pause = PauseAtQuery.After("agents.remove_permission.load");
        await using var services = pause.BuildServices();
        await new TestingServiceScope().StartGraph(out _)
            .AddAgent(out var agent, a => a.Permissions = [Permission.Names.ManageTenant])
            .BuildAsync();

        // Act
        var remove = new TestingServiceScope(services).SendAsync(
            new RemoveAgentPermission.Command(agent.Id, Permission.Names.ManageTenant));
        await pause.ReachedAsync();
        await new TestingServiceScope().SendAsync(new AddAgentPermission.Command(agent.Id, Permission.Names.ReadUsers));
        pause.Release();

        // Assert
        (await remove).Permissions.ShouldBe([Permission.Names.ReadUsers]);
        (await AgentPermissionsAsync(agent.Id)).ShouldBe([Permission.Names.ReadUsers]);
    }

    [Test]
    public async Task agent_delete_that_a_rival_permission_add_beat_after_the_load_runs_again_and_deletes()
    {
        // Arrange
        var pause = PauseAtQuery.After("agents.delete.load");
        await using var services = pause.BuildServices();
        await new TestingServiceScope().StartGraph(out _).AddAgent(out var agent).BuildAsync();

        // Act
        var delete = new TestingServiceScope(services).SendAsync(new DeleteAgent.Command(agent.Id));
        await pause.ReachedAsync();
        await new TestingServiceScope().SendAsync(new AddAgentPermission.Command(agent.Id, Permission.Names.ReadUsers));
        pause.Release();

        // Assert
        await delete;
        (await new TestingServiceScope().ExecuteDbContextAsync(db => db.Agents.IgnoreQueryFilters([QueryFilterNames.Tenant]).AnyAsync(a => a.Id == agent.Id)))
            .ShouldBeFalse();
    }

    [Test]
    public async Task tenant_delete_that_a_rival_update_beat_after_the_load_runs_again_and_deletes()
    {
        // Arrange
        var pause = PauseAtQuery.After("tenants.delete.load");
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
        await rival.SendAsync(new UpdateTenant.Command(tenant.Id,
            new TenantForUpdateDto { Name = $"Tenant {Guid.NewGuid():N}", Version = tenant.Version }));
        pause.Release();

        // Assert
        await delete;
        var saved = await new TestingServiceScope().ExecuteDbContextAsync(db =>
            db.Tenants.IgnoreQueryFilters().SingleAsync(t => t.Id == tenant.Id));
        saved.IsDeleted.ShouldBeTrue();
    }

    private static Task<List<string>> UserPermissionsAsync(string userId)
        => new TestingServiceScope().ExecuteDbContextAsync(db => db.UserPermissions
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .Where(p => p.UserId == userId)
            .Select(p => p.Permission.Value)
            .ToListAsync());

    private static Task<List<string>> AgentPermissionsAsync(string agentId)
        => new TestingServiceScope().ExecuteDbContextAsync(db => db.AgentPermissions
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .Where(p => p.AgentId == agentId)
            .Select(p => p.Permission.Value)
            .ToListAsync());
}
