namespace FullstackTemplate.IntegrationTests.FeatureTests.Users;

using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.Features;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.Server.Services;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

/// <summary>
/// The rows of user_permissions decide what a user can do, not the role. Each kind of caller gets the outcome
/// of its variant of <see cref="Caller"/>, and a caller can give only the access that it holds.
/// </summary>
public class PermissionGuardTests : TestBase
{
    [Test]
    public async Task user_role_member_granted_manage_users_can_delete_a_user()
    {
        // Arrange
        var (scope, _, target) = await ArrangeCallerWithOnlyAsync("User", Permission.ManageUsers());

        // Act
        await scope.SendAsync(new DeleteUser.Command(target.Id));

        // Assert
        (await UserExistsAsync(target.Id)).ShouldBeFalse();
    }

    [Test]
    public async Task user_role_member_without_manage_users_cannot_delete_a_user()
    {
        // Arrange
        var (scope, _, target) = await ArrangeCallerWithOnlyAsync("User");

        // Act
        var act = () => scope.SendAsync(new DeleteUser.Command(target.Id));

        // Assert
        (await act.ShouldThrowAsync<ForbiddenAccessException>()).Message
            .ShouldBe("You need the manage_users permission to do this.");
        (await UserExistsAsync(target.Id)).ShouldBeTrue();
    }

    [Test]
    public async Task admin_without_manage_users_cannot_delete_a_user()
    {
        // Arrange
        var (scope, _, target) = await ArrangeCallerWithOnlyAsync("Admin", Permission.ManageUserAccess(), Permission.ManageTenant());

        // Act
        var act = () => scope.SendAsync(new DeleteUser.Command(target.Id));

        // Assert
        await act.ShouldThrowAsync<ForbiddenAccessException>();
        (await UserExistsAsync(target.Id)).ShouldBeTrue();
    }

    [Test]
    public async Task a_revoked_permission_stops_on_the_next_request()
    {
        // Arrange
        var (scope, member, target) = await ArrangeCallerWithOnlyAsync("User", Permission.ManageUsers(), Permission.ReadUsers());
        var updated = await scope.SendAsync(
            new UpdateUser.Command(target.Id, new FakeUserForUpdateDto().Generate() with { Version = target.Version }));
        await new TestingServiceScope().SendAsync(new RemoveUserPermission.Command(member.Id, "manage_users"));

        var nextRequest = new TestingServiceScope();
        nextRequest.SetUserWithDefaults(identifier: member.Identifier);

        // Act
        var act = () => nextRequest.SendAsync(
            new UpdateUser.Command(target.Id, new FakeUserForUpdateDto().Generate() with { Version = updated.Version }));

        // Assert
        await act.ShouldThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task trusted_machine_client_can_delete_a_user()
    {
        // Arrange
        var (scope, _, target) = await ArrangeCallerWithOnlyAsync("User");
        scope.SetMachineClient(TestFixture.TrustedMachineClientId);

        // Act
        await scope.SendAsync(new DeleteUser.Command(target.Id));

        // Assert
        (await UserExistsAsync(target.Id)).ShouldBeFalse();
    }

    [Test]
    [Arguments(TestFixture.ReadOnlyMachineClientId, "This client has no permissions. It can read only the data that needs no permission.")]
    [Arguments("untrusted-machine", "This client is not a registered agent of your tenant. A tenant admin can register it.")]
    public async Task machine_client_that_cannot_manage_all_tenants_cannot_delete_a_user(string clientId, string denial)
    {
        // Arrange
        var (scope, _, target) = await ArrangeCallerWithOnlyAsync("User");
        scope.SetMachineClient(clientId);

        // Act
        var act = () => scope.SendAsync(new DeleteUser.Command(target.Id));

        // Assert
        (await act.ShouldThrowAsync<ForbiddenAccessException>()).Message.ShouldBe(denial);
        (await UserExistsAsync(target.Id)).ShouldBeTrue();
    }

    [Test]
    public async Task anonymous_caller_cannot_delete_a_user()
    {
        // Arrange
        var (scope, _, target) = await ArrangeCallerWithOnlyAsync("User");
        scope.SetUserUnauthenticated();

        // Act
        var act = () => scope.SendAsync(new DeleteUser.Command(target.Id));

        // Assert
        (await act.ShouldThrowAsync<ForbiddenAccessException>()).Message.ShouldBe("Sign in to do this.");
        (await UserExistsAsync(target.Id)).ShouldBeTrue();
    }

    [Test]
    public async Task signed_in_caller_with_no_user_record_cannot_delete_a_user()
    {
        // Arrange
        var (scope, _, target) = await ArrangeCallerWithOnlyAsync("User");
        scope.SetUserWithDefaults(identifier: $"no-record-{Guid.NewGuid()}");

        // Act
        var act = () => scope.SendAsync(new DeleteUser.Command(target.Id));

        // Assert
        (await act.ShouldThrowAsync<ForbiddenAccessException>()).Message
            .ShouldBe("You need a user record in a tenant to do this.");
        (await UserExistsAsync(target.Id)).ShouldBeTrue();
    }

    [Test]
    public async Task system_scope_can_delete_a_user_even_when_its_user_has_no_permission()
    {
        // Arrange
        var (_, member, target) = await ArrangeCallerWithOnlyAsync("User");
        await using var systemScope = TestFixture.BaseScopeFactory.CreateSystemScope();
        systemScope.ServiceProvider.GetRequiredService<ICurrentUserService>().UserIdentifier.Returns(member.Identifier);

        // Act
        await systemScope.ServiceProvider.GetRequiredService<ISender>().Send(new DeleteUser.Command(target.Id));

        // Assert
        (await UserExistsAsync(target.Id)).ShouldBeFalse();
    }

    [Test]
    public async Task member_without_manage_users_can_update_their_own_record()
    {
        // Arrange
        var (scope, member, _) = await ArrangeCallerWithOnlyAsync("User");
        var dto = new FakeUserForUpdateDto().Generate() with { Version = member.Version };

        // Act
        var updated = await scope.SendAsync(new UpdateUser.Command(member.Id, dto));

        // Assert
        updated.FirstName.ShouldBe(dto.FirstName);
    }

    [Test]
    public async Task member_without_manage_users_cannot_update_another_user()
    {
        // Arrange
        var (scope, _, target) = await ArrangeCallerWithOnlyAsync("User");

        // Act
        var act = () => scope.SendAsync(new UpdateUser.Command(target.Id, new FakeUserForUpdateDto().Generate()));

        // Assert
        await act.ShouldThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task holder_of_only_manage_users_can_add_a_user_role_user()
    {
        // Arrange
        var (scope, _, _) = await ArrangeCallerWithOnlyAsync("User", Permission.ManageUsers());
        var dto = new FakeUserForCreationDto().RuleFor(x => x.Role, _ => "User").Generate();

        // Act
        var added = await scope.SendAsync(new AddUser.Command(dto));

        // Assert
        (await UserExistsAsync(added.Id)).ShouldBeTrue();
    }

    [Test]
    public async Task holder_of_only_manage_users_cannot_add_an_admin()
    {
        // Arrange
        var (scope, _, _) = await ArrangeCallerWithOnlyAsync("User", Permission.ManageUsers());
        var dto = new FakeUserForCreationDto().RuleFor(x => x.Role, _ => "Admin").Generate();

        // Act
        var act = () => scope.SendAsync(new AddUser.Command(dto));

        // Assert
        await act.ShouldThrowAsync<ForbiddenAccessException>();
        var added = await scope.ExecuteDbContextAsync(db => db.Users.IgnoreQueryFilters()
            .AnyAsync(u => u.Identifier == dto.Identifier));
        added.ShouldBeFalse();
    }

    [Test]
    public async Task invalid_role_from_a_holder_of_manage_users_is_a_validation_error()
    {
        // Arrange
        var (scope, _, _) = await ArrangeCallerWithOnlyAsync("User", Permission.ManageUsers());
        var dto = new FakeUserForCreationDto().RuleFor(x => x.Role, _ => "NotARole").Generate();

        // Act
        var act = () => scope.SendAsync(new AddUser.Command(dto));

        // Assert
        await act.ShouldThrowAsync<ValidationException>();
    }

    [Test]
    public async Task invalid_role_from_a_caller_without_manage_users_is_forbidden()
    {
        // Arrange
        var (scope, _, _) = await ArrangeCallerWithOnlyAsync("User");
        var dto = new FakeUserForCreationDto().RuleFor(x => x.Role, _ => "NotARole").Generate();

        // Act
        var act = () => scope.SendAsync(new AddUser.Command(dto));

        // Assert
        await act.ShouldThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task holder_of_only_manage_user_access_and_read_users_can_grant_manage_user_access()
    {
        // Arrange
        var (scope, _, target) = await ArrangeCallerWithOnlyAsync("User", Permission.ManageUserAccess(), Permission.ReadUsers());

        // Act
        var updated = await scope.SendAsync(new AddUserPermission.Command(target.Id, "manage_user_access"));

        // Assert
        updated.Permissions.ShouldBe(["manage_user_access"]);
    }

    [Test]
    public async Task holder_of_only_manage_user_access_and_read_users_cannot_grant_manage_tenant()
    {
        // Arrange
        var (scope, _, target) = await ArrangeCallerWithOnlyAsync("User", Permission.ManageUserAccess(), Permission.ReadUsers());

        // Act
        var act = () => scope.SendAsync(new AddUserPermission.Command(target.Id, "manage_tenant"));

        // Assert
        (await act.ShouldThrowAsync<ForbiddenAccessException>()).Message
            .ShouldBe("You need the manage_tenant permission to do this.");
        (await PermissionsOfAsync(target.Id)).ShouldBeEmpty();
    }

    [Test]
    public async Task invalid_permission_from_a_holder_of_manage_user_access_is_a_validation_error()
    {
        // Arrange
        var (scope, _, target) = await ArrangeCallerWithOnlyAsync("User", Permission.ManageUserAccess(), Permission.ReadUsers());

        // Act
        var act = () => scope.SendAsync(new AddUserPermission.Command(target.Id, "not_a_permission"));

        // Assert
        await act.ShouldThrowAsync<ValidationException>();
    }

    [Test]
    public async Task holder_of_only_manage_user_access_and_read_users_cannot_make_a_user_an_admin()
    {
        // Arrange
        var (scope, _, target) = await ArrangeCallerWithOnlyAsync("User", Permission.ManageUserAccess(), Permission.ReadUsers());

        // Act
        var act = () => scope.SendAsync(new UpdateUserRole.Command(target.Id, "Admin"));

        // Assert
        await act.ShouldThrowAsync<ForbiddenAccessException>();
        var userInDb = await new TestingServiceScope().ExecuteDbContextAsync(db => db.Users
            .SingleAsync(u => u.Id == target.Id));
        userInDb.Role.Value.ShouldBe("User");
    }

    [Test]
    public async Task holder_of_only_manage_user_access_and_read_users_cannot_demote_an_admin()
    {
        // Arrange
        var (scope, _, _) = await ArrangeCallerWithOnlyAsync("User", Permission.ManageUserAccess(), Permission.ReadUsers());
        var admin = await AddAdminAsync();

        // Act
        var act = () => scope.SendAsync(new UpdateUserRole.Command(admin.Id, "User"));

        // Assert
        await act.ShouldThrowAsync<ForbiddenAccessException>();
        (await PermissionsOfAsync(admin.Id)).ShouldBe(Permission.ListNames(), ignoreOrder: true);
    }

    [Test]
    public async Task holder_of_only_manage_user_access_and_read_users_cannot_take_manage_tenant_away()
    {
        // Arrange
        var (scope, _, _) = await ArrangeCallerWithOnlyAsync("User", Permission.ManageUserAccess(), Permission.ReadUsers());
        var admin = await AddAdminAsync();

        // Act
        var act = () => scope.SendAsync(new RemoveUserPermission.Command(admin.Id, "manage_tenant"));

        // Assert
        (await act.ShouldThrowAsync<ForbiddenAccessException>()).Message
            .ShouldBe("You need the manage_tenant permission to do this.");
        (await PermissionsOfAsync(admin.Id)).ShouldContain("manage_tenant");
    }

    [Test]
    public async Task holder_of_only_manage_user_access_and_read_users_can_take_manage_user_access_away()
    {
        // Arrange
        var (scope, _, _) = await ArrangeCallerWithOnlyAsync("User", Permission.ManageUserAccess(), Permission.ReadUsers());
        var admin = await AddAdminAsync();

        // Act
        var updated = await scope.SendAsync(new RemoveUserPermission.Command(admin.Id, "manage_user_access"));

        // Assert
        updated.Permissions.ShouldBe(Permission.ListNames().Except(["manage_user_access"]), ignoreOrder: true);
    }

    [Test]
    public async Task holder_of_only_manage_users_cannot_delete_an_admin()
    {
        // Arrange
        var (scope, _, _) = await ArrangeCallerWithOnlyAsync("User", Permission.ManageUsers());
        var admin = await AddAdminAsync();

        // Act
        var act = () => scope.SendAsync(new DeleteUser.Command(admin.Id));

        // Assert
        await act.ShouldThrowAsync<ForbiddenAccessException>();
        (await UserExistsAsync(admin.Id)).ShouldBeTrue();
    }

    [Test]
    public async Task caller_without_manage_user_access_cannot_add_or_remove_a_permission()
    {
        // Arrange
        var (scope, _, _) = await ArrangeCallerWithOnlyAsync("User", Permission.ManageUsers(), Permission.ManageTenant());
        var admin = await AddAdminAsync();

        // Act
        var add = () => scope.SendAsync(new AddUserPermission.Command(admin.Id, "manage_tenant"));
        var remove = () => scope.SendAsync(new RemoveUserPermission.Command(admin.Id, "manage_tenant"));

        // Assert
        (await add.ShouldThrowAsync<ForbiddenAccessException>()).Message
            .ShouldBe("You need the manage_user_access permission to do this.");
        (await remove.ShouldThrowAsync<ForbiddenAccessException>()).Message
            .ShouldBe("You need the manage_user_access permission to do this.");
        (await PermissionsOfAsync(admin.Id)).ShouldBe(Permission.ListNames(), ignoreOrder: true);
    }

    [Test]
    public async Task deleted_user_is_denied_even_with_the_permission_rows()
    {
        // Arrange
        var (scope, member, target) = await ArrangeCallerWithOnlyAsync("User", Permission.ManageUsers());
        await new TestingServiceScope().SendAsync(new DeleteUser.Command(member.Id));

        // Act
        var act = () => scope.SendAsync(new DeleteUser.Command(target.Id));

        // Assert
        (await act.ShouldThrowAsync<ForbiddenAccessException>()).Message
            .ShouldBe("You need a user record in a tenant to do this.");
        (await UserExistsAsync(target.Id)).ShouldBeTrue();
    }

    [Test]
    public async Task get_caller_gives_a_member_its_permission_rows_and_not_the_defaults_of_its_role()
    {
        // Arrange
        var (scope, member, _) = await ArrangeCallerWithOnlyAsync("Admin", Permission.ManageUsers(), Permission.ManageTenant());
        var guard = scope.GetService<IPermissionGuard>();

        // Act
        var caller = await guard.GetCallerAsync(CancellationToken.None);

        // Assert
        var memberCaller = caller.ShouldBeOfType<Caller.Member>();
        memberCaller.UserId.ShouldBe(member.Id);
        caller.Permissions.ShouldBe([Permission.ManageUsers(), Permission.ManageTenant()], ignoreOrder: true);
        caller.Has(Permission.ManageUserAccess()).ShouldBeFalse();
    }

    private static async Task<(TestingServiceScope Scope, User Caller, User Target)> ArrangeCallerWithOnlyAsync(
        string role, params Permission[] permissions)
    {
        var scope = new TestingServiceScope();
        var graph = scope.StartGraph(out _)
            .AddUsers(out var users, u => u.Role = role, u => u.Role = "User");
        var caller = users[0];
        foreach (var permission in Permission.GetAll())
            caller.RemovePermission(permission);
        caller.AddPermissions(permissions);
        await graph.BuildAsync();

        scope.SetUserWithDefaults(identifier: caller.Identifier);
        return (scope, caller, users[1]);
    }

    private static async Task<User> AddAdminAsync()
    {
        var scope = new TestingServiceScope();
        await scope.StartGraph(out _).AddUser(out var admin, u => u.Role = "Admin").BuildAsync();
        return admin;
    }

    private static Task<bool> UserExistsAsync(string userId)
        => new TestingServiceScope().ExecuteDbContextAsync(db => db.Users.AnyAsync(u => u.Id == userId));

    private static Task<List<string>> PermissionsOfAsync(string userId)
        => new TestingServiceScope().ExecuteDbContextAsync(db => db.UserPermissions
            .Where(p => p.UserId == userId).Select(p => p.Permission.Value).ToListAsync());
}
