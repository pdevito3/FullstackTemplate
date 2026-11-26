namespace FullstackTemplate.UnitTests.Domain.Users;

using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.DomainEvents;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using Shouldly;

public class UpdateUserRolesTests
{
    [Test]
    public void new_roles_give_their_permissions_and_queue_user_updated()
    {
        // Arrange
        var user = User.Create(new FakeUserForCreation().Generate() with { Role = UserRole.User().Value });
        user.AddPermission(Permission.ManageAgents());

        // Act
        user.UpdateRoles(Kept(user, UserRole.Admin()));

        // Assert
        user.CurrentRoles().ShouldBe([UserRole.Admin()]);
        user.UserPermissions.Select(p => p.Permission).ShouldBe(UserRole.Admin().GetPermissions(), ignoreOrder: true);
        user.DomainEvents.ShouldBe([new UserCreated(user.Id), new UserUpdated(user.Id)]);
    }

    [Test]
    public void the_same_roles_keep_the_permissions_and_queue_no_event()
    {
        // Arrange
        var user = User.Create(new FakeUserForCreation().Generate() with { Role = UserRole.User().Value });
        user.AddPermission(Permission.ManageAgents());
        var permissions = user.UserPermissions.ToList();

        // Act
        user.UpdateRoles(Kept(user, UserRole.User()));

        // Assert
        user.UserPermissions.ShouldBe(permissions);
        user.DomainEvents.ShouldBe([new UserCreated(user.Id)]);
    }

    [Test]
    public void a_permission_added_by_hand_stays_after_the_roles_change_and_change_back()
    {
        // Arrange
        var user = User.Create(new FakeUserForCreation().Generate() with { Role = UserRole.User().Value });
        user.AddPermission(Permission.ReadUsers());

        // Act
        user.ChangeRolesIgnoringLastManager(UserRole.Admin());
        user.ChangeRolesIgnoringLastManager(UserRole.User());

        // Assert
        user.CurrentRoles().ShouldBe([UserRole.User()]);
        user.UserPermissions.Select(p => p.Permission).ShouldBe([Permission.ReadUsers()]);
    }

    [Test]
    public void a_permission_removed_by_hand_stays_removed_when_roles_that_give_it_are_set()
    {
        // Arrange
        var user = User.Create(new FakeUserForCreation().Generate() with { Role = UserRole.Admin().Value });
        user.RemovePermissionIgnoringLastManager(Permission.ManageAgents());

        // Act
        user.ChangeRolesIgnoringLastManager(UserRole.Admin(), UserRole.User());

        // Assert
        user.CurrentRoles().ShouldBe([UserRole.Admin(), UserRole.User()], ignoreOrder: true);
        user.HasPermission(Permission.ManageAgents()).ShouldBeFalse();
        user.UserPermissions.Select(p => p.Permission)
            .ShouldBe(UserRole.Admin().GetPermissions().Where(p => p != Permission.ManageAgents()), ignoreOrder: true);
    }

    [Test]
    public void an_add_after_a_removal_of_the_same_permission_grants_it_again()
    {
        // Arrange
        var user = User.Create(new FakeUserForCreation().Generate() with { Role = UserRole.Admin().Value });
        user.RemovePermissionIgnoringLastManager(Permission.ManageAgents());

        // Act
        user.AddPermission(Permission.ManageAgents());

        // Assert
        user.HasPermission(Permission.ManageAgents()).ShouldBeTrue();
        user.PermissionOverrides.Select(o => (o.Permission, o.Granted)).ShouldBe([(Permission.ManageAgents(), true)]);
        user.ChangeRolesIgnoringLastManager(UserRole.User());
        user.UserPermissions.Select(p => p.Permission).ShouldBe([Permission.ManageAgents()]);
    }

    [Test]
    public void two_roles_give_the_union_of_their_permissions_with_the_overrides()
    {
        // Arrange
        var user = User.Create(new FakeUserForCreation().Generate() with { Role = UserRole.User().Value });
        user.RemovePermissionIgnoringLastManager(Permission.ManageTenant());

        // Act
        user.ChangeRolesIgnoringLastManager(UserRole.User(), UserRole.Admin());

        // Assert
        user.CurrentRoles().ShouldBe([UserRole.User(), UserRole.Admin()], ignoreOrder: true);
        UserRole.PermissionsOf([UserRole.User(), UserRole.Admin()])
            .ShouldBe(UserRole.Admin().GetPermissions(), ignoreOrder: true);
        user.UserPermissions.Select(p => p.Permission)
            .ShouldBe(UserRole.Admin().GetPermissions().Where(p => p != Permission.ManageTenant()), ignoreOrder: true);
    }

    [Test]
    public void the_proof_of_another_user_or_of_a_removal_is_refused()
    {
        // Arrange
        var user = User.Create(new FakeUserForCreation().Generate() with { Role = UserRole.User().Value });
        var other = User.Create(new FakeUserForCreation().Generate() with { Role = UserRole.User().Value });
        var removal = ((ManagerDecision.Kept)TenantManagers.Decide(new ManagerCase(user, new ManagerChange.Removal(), []))).Proof;

        // Act
        var ofOther = () => user.UpdateRoles(Kept(other, UserRole.Admin()));
        var ofRemoval = () => user.UpdateRoles(removal);

        // Assert
        ofOther.ShouldThrow<ArgumentException>();
        ofRemoval.ShouldThrow<ArgumentException>();
        user.CurrentRoles().ShouldBe([UserRole.User()]);
    }

    private static ManagerKept Kept(User user, params UserRole[] roles)
        => ((ManagerDecision.Kept)TenantManagers.Decide(new ManagerCase(user, new ManagerChange.NewRoles(roles.ToHashSet()),
            user.CanManageTenant ? [user.Id, "usr_other"] : []))).Proof;
}
