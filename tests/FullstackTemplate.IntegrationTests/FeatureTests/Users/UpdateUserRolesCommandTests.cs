namespace FullstackTemplate.IntegrationTests.FeatureTests.Users;

using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.Features;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.Server.Resources;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Microsoft.EntityFrameworkCore;
using Shouldly;

public class UpdateUserRolesCommandTests : TestBase
{
    [Test]
    public async Task member_cannot_change_a_role()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddUser(out var member, u => u.Role = "User")
            .BuildAsync();
        testingServiceScope.SetUserWithDefaults(identifier: member.Identifier);

        // Act
        var act = () => testingServiceScope.SendAsync(new UpdateUserRoles.Command(member.Id, ["Admin"]));

        // Assert
        await act.ShouldThrowAsync<ForbiddenAccessException>();
        (await RolesOfAsync(member.Id)).ShouldBe(["User"]);
    }

    [Test]
    public async Task can_change_role_from_user_to_admin()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddUser(out var createdUser, u => u.Role = "User")
            .BuildAsync();

        // Act
        var command = new UpdateUserRoles.Command(createdUser.Id, ["Admin"]);
        var userReturned = await testingServiceScope.SendAsync(command);

        // Assert
        userReturned.Roles.ShouldBe(["Admin"]);
        userReturned.Permissions.ShouldContain("manage_tenant");
    }

    [Test]
    public async Task changing_role_to_admin_grants_admin_permissions_in_db()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddUser(out var createdUser, u => u.Role = "User")
            .BuildAsync();

        // Act
        var command = new UpdateUserRoles.Command(createdUser.Id, ["Admin"]);
        await testingServiceScope.SendAsync(command);

        // Assert
        var userInDb = await testingServiceScope.ExecuteDbContextAsync(db =>
            db.Users
                .Include(u => u.UserPermissions)
                .FirstOrDefaultAsync(u => u.Id == createdUser.Id));

        userInDb.ShouldNotBeNull();
        (await RolesOfAsync(createdUser.Id)).ShouldBe(["Admin"]);
        userInDb.HasPermission(Permission.ManageTenant()).ShouldBeTrue();
    }

    [Test]
    public async Task can_change_role_from_admin_to_user()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddUser(out var createdUser, u => u.Role = "Admin")
            .BuildAsync();

        // Act
        var command = new UpdateUserRoles.Command(createdUser.Id, ["User"]);
        var userReturned = await testingServiceScope.SendAsync(command);

        // Assert
        userReturned.Roles.ShouldBe(["User"]);
        userReturned.Permissions.ShouldBeEmpty();
    }

    [Test]
    public async Task changing_role_from_admin_to_user_removes_permissions()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddUser(out var createdUser, u => u.Role = "Admin")
            .BuildAsync();

        // Verify admin has permissions before role change
        createdUser.UserPermissions.ShouldNotBeEmpty();

        // Act
        var command = new UpdateUserRoles.Command(createdUser.Id, ["User"]);
        await testingServiceScope.SendAsync(command);

        // Assert
        var userAfter = await testingServiceScope.ExecuteDbContextAsync(db =>
            db.Users
                .Include(u => u.UserPermissions)
                .FirstOrDefaultAsync(u => u.Id == createdUser.Id));

        userAfter.ShouldNotBeNull();
        (await RolesOfAsync(createdUser.Id)).ShouldBe(["User"]);
        userAfter.UserPermissions.ShouldBeEmpty();
    }

    [Test]
    public async Task changing_roles_keeps_the_permissions_added_and_removed_by_hand()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddUsers(out var users, u => u.Role = "Admin", u => u.Role = "User")
            .BuildAsync();
        var (admin, member) = (users[0], users[1]);
        await testingServiceScope.SendAsync(new AddUserPermission.Command(member.Id, "read_users"));
        await testingServiceScope.SendAsync(new RemoveUserPermission.Command(admin.Id, "manage_agents"));

        // Act
        await testingServiceScope.SendAsync(new UpdateUserRoles.Command(member.Id, ["Admin"]));
        var memberReturned = await testingServiceScope.SendAsync(new UpdateUserRoles.Command(member.Id, ["User"]));
        var adminReturned = await testingServiceScope.SendAsync(new UpdateUserRoles.Command(admin.Id, ["Admin", "User"]));

        // Assert
        memberReturned.Roles.ShouldBe(["User"]);
        memberReturned.Permissions.ShouldBe(["read_users"]);
        adminReturned.Roles.ShouldBe(["Admin", "User"], ignoreOrder: true);
        adminReturned.Permissions.ShouldBe(Permission.ListNames().Where(p => p != "manage_agents"), ignoreOrder: true);
        (await PermissionsOfAsync(member.Id)).ShouldBe(["read_users"]);
        (await RolesOfAsync(admin.Id)).ShouldBe(["Admin", "User"], ignoreOrder: true);
    }

    [Test]
    public async Task throws_when_user_not_found()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var nonExistentId = EntityId.New(User.IdPrefix);

        // Act & Assert
        var command = new UpdateUserRoles.Command(nonExistentId, ["Admin"]);
        await Should.ThrowAsync<Exception>(async () =>
            await testingServiceScope.SendAsync(command));
    }

    [Test]
    [Arguments(new[] { "InvalidRole" }, "roles[0]")]
    [Arguments(new[] { "User", "InvalidRole" }, "roles[1]")]
    public async Task an_invalid_role_is_a_validation_error_keyed_by_its_index(string[] roles, string key)
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddUser(out var createdUser, u => u.Role = "User")
            .BuildAsync();

        // Act
        var act = () => testingServiceScope.SendAsync(new UpdateUserRoles.Command(createdUser.Id, roles));

        // Assert
        (await act.ShouldThrowAsync<ValidationException>()).Property.ShouldBe(key);
        (await RolesOfAsync(createdUser.Id)).ShouldBe(["User"]);
    }

    [Test]
    public async Task a_caller_who_lacks_a_permission_of_an_added_role_is_refused()
    {
        // Arrange
        var (scope, target) = await ArrangeCallerWithAllButAsync(Permission.ManageAgents());

        // Act
        var act = () => scope.SendAsync(new UpdateUserRoles.Command(target.Id, ["User", "Admin"]));

        // Assert
        (await act.ShouldThrowAsync<ForbiddenAccessException>()).Message
            .ShouldBe("You cannot manage agents. Ask a tenant admin for access.");
        (await RolesOfAsync(target.Id)).ShouldBe(["User"]);
        (await PermissionsOfAsync(target.Id)).ShouldBeEmpty();
    }

    [Test]
    public async Task a_caller_who_holds_each_permission_of_the_user_but_not_those_of_an_added_role_is_refused()
    {
        // Arrange
        var scope = new TestingServiceScope();
        var graph = scope.StartGraph(out _)
            .AddUsers(out var users, u => u.Role = "User", u => u.Role = "User");
        var (caller, target) = (users[0], users[1]);
        caller.AddPermissions([Permission.ManageUserAccess(), Permission.ReadUsers()]);
        target.AddPermission(Permission.ReadUsers());
        await graph.BuildAsync();
        scope.SetUserWithDefaults(identifier: caller.Identifier);

        // Act
        var act = () => scope.SendAsync(new UpdateUserRoles.Command(target.Id, ["User", "Admin"]));

        // Assert
        await act.ShouldThrowAsync<ForbiddenAccessException>();
        (await RolesOfAsync(target.Id)).ShouldBe(["User"]);
        (await PermissionsOfAsync(target.Id)).ShouldBe(["read_users"]);
    }

    [Test]
    [Property("Rule", "UM-4")]
    public async Task no_roles_for_the_only_manager_is_refused_by_the_last_manager_rule()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddTenant(out _)
            .AddUser(out var admin, u => u.Role = "Admin")
            .BuildAsync();
        testingServiceScope.SetUserWithDefaults(identifier: admin.Identifier);

        // Act
        var act = () => testingServiceScope.SendAsync(new UpdateUserRoles.Command(admin.Id, []));

        // Assert
        var refusal = await act.ShouldThrowAsync<ValidationException>();
        refusal.Property.ShouldBe("roles");
        refusal.Message.ShouldBe(TenantManagers.LastManagerMessage);
        (await RolesOfAsync(admin.Id)).ShouldBe(["Admin"]);
        (await PermissionsOfAsync(admin.Id)).ShouldBe(Permission.ListNames(), ignoreOrder: true);
    }

    [Test]
    public async Task setting_the_same_role_keeps_the_permissions()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddUser(out var createdUser, u => u.Role = "Admin")
            .BuildAsync();
        await testingServiceScope.SendAsync(new RemoveUserPermission.Command(createdUser.Id, "manage_tenant"));

        // Act
        var userReturned = await testingServiceScope.SendAsync(new UpdateUserRoles.Command(createdUser.Id, ["Admin"]));

        // Assert
        userReturned.Permissions.ShouldNotContain("manage_tenant");
        var saved = await testingServiceScope.SendAsync(new GetUser.Query(createdUser.Id));
        saved.Permissions.ShouldNotContain("manage_tenant");
    }

    private static async Task<(TestingServiceScope Scope, User Target)> ArrangeCallerWithAllButAsync(Permission missing)
    {
        var scope = new TestingServiceScope();
        var graph = scope.StartGraph(out _)
            .AddUsers(out var users, u => u.Role = "User", u => u.Role = "User");
        var caller = users[0];
        caller.AddPermissions(Permission.GetAll().Where(p => p != missing));
        await graph.BuildAsync();
        scope.SetUserWithDefaults(identifier: caller.Identifier);
        return (scope, users[1]);
    }

    private static Task<List<string>> RolesOfAsync(string userId)
        => new TestingServiceScope().ExecuteDbContextAsync(db => db.UserRoles.IgnoreQueryFilters([QueryFilterNames.Tenant])
            .Where(r => r.UserId == userId).Select(r => r.Role.Value).ToListAsync());

    private static Task<List<string>> PermissionsOfAsync(string userId)
        => new TestingServiceScope().ExecuteDbContextAsync(db => db.UserPermissions.IgnoreQueryFilters([QueryFilterNames.Tenant])
            .Where(p => p.UserId == userId).Select(p => p.Permission.Value).ToListAsync());
}
