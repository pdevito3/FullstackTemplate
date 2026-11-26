namespace FullstackTemplate.IntegrationTests.FeatureTests.Users;

using FullstackTemplate.IntegrationTests.Databases;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.Features;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Utilities;
using Microsoft.EntityFrameworkCore;
using Shouldly;

public class LastManagerTests : TestBase
{
    [Test]
    [Property("Rule", "UM-4")]
    public async Task the_only_admin_cannot_become_a_user()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUsers(out var users, u => u.Role = "Admin", u => u.Role = "User")
            .BuildAsync();
        var admin = users[0];
        testingServiceScope.SetUserWithDefaults(identifier: admin.Identifier);

        // Act
        var act = () => testingServiceScope.SendAsync(new UpdateUserRoles.Command(admin.Id, ["User"]));

        // Assert
        var refusal = await act.ShouldThrowAsync<ValidationException>();
        refusal.Property.ShouldBe("roles");
        refusal.Message.ShouldBe(TenantManagers.LastManagerMessage);
        (await ManagerIdsAsync(testingServiceScope, tenant.Id)).ShouldBe([admin.Id]);
    }

    [Test]
    [Property("Rule", "UM-4")]
    public async Task the_only_admin_cannot_be_removed()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUser(out var admin, u => u.Role = "Admin")
            .BuildAsync();
        testingServiceScope.SetUserWithDefaults(identifier: admin.Identifier);

        // Act
        var act = () => testingServiceScope.SendAsync(new DeleteUser.Command(admin.Id));

        // Assert
        (await act.ShouldThrowAsync<ValidationException>()).Message.ShouldBe(TenantManagers.LastManagerMessage);
        (await ManagerIdsAsync(testingServiceScope, tenant.Id)).ShouldBe([admin.Id]);
    }

    [Test]
    [Property("Rule", "UM-4")]
    public async Task one_of_two_admins_can_become_a_user_and_the_other_can_then_be_removed_only_with_a_new_admin()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUsers(out var admins, u => u.Role = "Admin", u => u.Role = "Admin")
            .BuildAsync();
        var (ada, bea) = (admins[0], admins[1]);
        testingServiceScope.SetUserWithDefaults(identifier: ada.Identifier);

        // Act
        await testingServiceScope.SendAsync(new UpdateUserRoles.Command(bea.Id, ["User"]));
        var removeAda = () => testingServiceScope.SendAsync(new DeleteUser.Command(ada.Id));
        await removeAda.ShouldThrowAsync<ValidationException>();
        await testingServiceScope.SendAsync(new UpdateUserRoles.Command(bea.Id, ["Admin"]));
        await testingServiceScope.SendAsync(new DeleteUser.Command(ada.Id));

        // Assert
        (await ManagerIdsAsync(testingServiceScope, tenant.Id)).ShouldBe([bea.Id]);
    }

    [Test]
    [Property("Rule", "UM-4")]
    public async Task two_admins_who_demote_each_other_at_once_leave_one_admin()
    {
        // Arrange
        var pause = PauseAtQuery.After("users.load_managers");
        await using var services = pause.BuildServices();
        await new TestingServiceScope().StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUsers(out var admins, u => u.Role = "Admin", u => u.Role = "Admin")
            .BuildAsync();
        var (ada, bea) = (admins[0], admins[1]);
        var underTest = new TestingServiceScope(services);
        var rival = new TestingServiceScope();
        underTest.SetUserWithDefaults(identifier: ada.Identifier);
        rival.SetUserWithDefaults(identifier: bea.Identifier);
        var demoteBea = underTest.SendAsync(new UpdateUserRoles.Command(bea.Id, ["User"]));
        await pause.ReachedAsync();

        // Act
        var demoteAda = rival.SendAsync(new UpdateUserRoles.Command(ada.Id, ["User"]));
        try
        {
            await pause.WaitUntilBlockedAsync(demoteAda);
        }
        finally
        {
            pause.Release();
        }

        // Assert
        (await demoteBea).Roles.ShouldBe(["User"]);
        (await Should.ThrowAsync<ValidationException>(demoteAda)).Message.ShouldBe(TenantManagers.LastManagerMessage);
        (await ManagerIdsAsync(rival, tenant.Id)).ShouldBe([ada.Id]);
    }

    [Test]
    [Property("Rule", "UM-4")]
    public async Task two_admins_who_remove_each_other_at_once_leave_one_admin()
    {
        // Arrange
        var pause = PauseAtQuery.After("users.load_managers");
        await using var services = pause.BuildServices();
        await new TestingServiceScope().StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUsers(out var admins, u => u.Role = "Admin", u => u.Role = "Admin")
            .BuildAsync();
        var (ada, bea) = (admins[0], admins[1]);
        var underTest = new TestingServiceScope(services);
        var rival = new TestingServiceScope();
        underTest.SetUserWithDefaults(identifier: ada.Identifier);
        rival.SetUserWithDefaults(identifier: bea.Identifier);
        var removeBea = underTest.SendAsync(new DeleteUser.Command(bea.Id));
        await pause.ReachedAsync();

        // Act
        var removeAda = rival.SendAsync(new DeleteUser.Command(ada.Id));
        try
        {
            await pause.WaitUntilBlockedAsync(removeAda);
        }
        finally
        {
            pause.Release();
        }

        // Assert
        await removeBea;
        (await Should.ThrowAsync<ValidationException>(removeAda)).Message.ShouldBe(TenantManagers.LastManagerMessage);
        (await ManagerIdsAsync(rival, tenant.Id)).ShouldBe([ada.Id]);
    }

    [Test]
    [Property("Rule", "UM-4")]
    public async Task the_only_manager_cannot_remove_a_permission_from_their_own_user()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUsers(out var users, u => u.Role = "Admin", u => u.Role = "User")
            .BuildAsync();
        var admin = users[0];
        testingServiceScope.SetUserWithDefaults(identifier: admin.Identifier);

        // Act
        var act = () => testingServiceScope.SendAsync(
            new RemoveUserPermission.Command(admin.Id, Permission.Names.ManageUserAccess));

        // Assert
        var refusal = await act.ShouldThrowAsync<ValidationException>();
        refusal.Property.ShouldBe("permission");
        refusal.Message.ShouldBe(TenantManagers.LastManagerMessage);
        (await ManagerIdsAsync(testingServiceScope, tenant.Id)).ShouldBe([admin.Id]);
    }

    [Test]
    [Property("Rule", "UM-4")]
    public async Task an_admin_who_lacks_a_permission_does_not_count_so_the_other_admin_cannot_become_a_user()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUsers(out var admins, u => u.Role = "Admin", u => u.Role = "Admin")
            .BuildAsync();
        var (ada, bea) = (admins[0], admins[1]);
        testingServiceScope.SetUserWithDefaults(identifier: ada.Identifier);
        await testingServiceScope.SendAsync(new RemoveUserPermission.Command(bea.Id, Permission.Names.ReadAuditLog));

        // Act
        var demoteAda = () => testingServiceScope.SendAsync(new UpdateUserRoles.Command(ada.Id, ["User"]));
        var removeAda = () => testingServiceScope.SendAsync(new DeleteUser.Command(ada.Id));

        // Assert
        (await demoteAda.ShouldThrowAsync<ValidationException>()).Message.ShouldBe(TenantManagers.LastManagerMessage);
        (await removeAda.ShouldThrowAsync<ValidationException>()).Message.ShouldBe(TenantManagers.LastManagerMessage);
        (await ManagerIdsAsync(testingServiceScope, tenant.Id)).ShouldBe([ada.Id]);
    }

    [Test]
    [Property("Rule", "UM-4")]
    public async Task a_user_who_holds_every_permission_can_manage_so_the_only_admin_can_become_a_user()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUsers(out var users, u => u.Role = "Admin", u => u.Role = "User")
            .BuildAsync();
        var (admin, member) = (users[0], users[1]);
        testingServiceScope.SetUserWithDefaults(identifier: admin.Identifier);
        foreach (var permission in Permission.ListNames())
            await testingServiceScope.SendAsync(new AddUserPermission.Command(member.Id, permission));

        // Act
        var result = await testingServiceScope.SendAsync(new UpdateUserRoles.Command(admin.Id, ["User"]));

        // Assert
        result.Roles.ShouldBe(["User"]);
        (await ManagerIdsAsync(testingServiceScope, tenant.Id)).ShouldBe([member.Id]);
    }

    [Test]
    [Property("Rule", "UM-4")]
    public async Task two_managers_who_remove_a_permission_from_each_other_at_once_leave_one_manager()
    {
        // Arrange
        var pause = PauseAtQuery.After("users.load_managers");
        await using var services = pause.BuildServices();
        await new TestingServiceScope().StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUsers(out var admins, u => u.Role = "Admin", u => u.Role = "Admin")
            .BuildAsync();
        var (ada, bea) = (admins[0], admins[1]);
        var underTest = new TestingServiceScope(services);
        var rival = new TestingServiceScope();
        underTest.SetUserWithDefaults(identifier: ada.Identifier);
        rival.SetUserWithDefaults(identifier: bea.Identifier);
        var fromBea = underTest.SendAsync(new RemoveUserPermission.Command(bea.Id, Permission.Names.ManageUsers));
        await pause.ReachedAsync();

        // Act
        var fromAda = rival.SendAsync(new RemoveUserPermission.Command(ada.Id, Permission.Names.ManageUsers));
        try
        {
            await pause.WaitUntilBlockedAsync(fromAda);
        }
        finally
        {
            pause.Release();
        }

        // Assert
        await fromBea;
        (await Should.ThrowAsync<ValidationException>(fromAda)).Message.ShouldBe(TenantManagers.LastManagerMessage);
        (await ManagerIdsAsync(rival, tenant.Id)).ShouldBe([ada.Id]);
    }

    [Test]
    [Property("Rule", "UM-4")]
    public async Task a_removal_that_a_grant_made_a_manager_after_the_load_runs_again_and_saves()
    {
        // Arrange
        var pause = PauseAtQuery.After("users.remove_permission.load");
        await using var services = pause.BuildServices();
        var arrange = new TestingServiceScope();
        await arrange.StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUsers(out var admins, u => u.Role = "Admin", u => u.Role = "Admin")
            .BuildAsync();
        var (ada, bea) = (admins[0], admins[1]);
        arrange.SetUserWithDefaults(identifier: ada.Identifier);
        await arrange.SendAsync(new RemoveUserPermission.Command(bea.Id, Permission.Names.ReadAuditLog));
        var underTest = new TestingServiceScope(services);
        underTest.SetUserWithDefaults(identifier: ada.Identifier);
        var removal = underTest.SendAsync(new RemoveUserPermission.Command(bea.Id, Permission.Names.ReadAgents));
        await pause.ReachedAsync();

        // Act
        try
        {
            await arrange.SendAsync(new AddUserPermission.Command(bea.Id, Permission.Names.ReadAuditLog));
        }
        finally
        {
            pause.Release();
        }

        // Assert
        var result = await removal;
        result.Permissions.ShouldContain(Permission.Names.ReadAuditLog);
        result.Permissions.ShouldNotContain(Permission.Names.ReadAgents);
        (await ManagerIdsAsync(arrange, tenant.Id)).ShouldBe([ada.Id]);
    }

    private static async Task<List<string>> ManagerIdsAsync(TestingServiceScope scope, string tenantId)
    {
        var every = Permission.ListNames();
        var held = await scope.ExecuteDbContextAsync(db => db.UserPermissions
            .IgnoreQueryFilters()
            .Where(p => !p.IsDeleted && p.User.TenantId == tenantId && !p.User.IsDeleted)
            .Select(p => new { p.UserId, Permission = p.Permission.Value })
            .ToListAsync());
        return held.GroupBy(p => p.UserId)
            .Where(g => every.All(name => g.Any(p => p.Permission == name)))
            .Select(g => g.Key)
            .ToList();
    }
}
