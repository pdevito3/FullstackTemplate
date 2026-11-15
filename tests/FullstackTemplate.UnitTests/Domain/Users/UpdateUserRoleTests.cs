namespace FullstackTemplate.UnitTests.Domain.Users;

using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.DomainEvents;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using Shouldly;

public class UpdateUserRoleTests
{
    [Test]
    public void a_new_role_resets_the_permissions_and_queues_user_updated()
    {
        // Arrange
        var user = User.Create(new FakeUserForCreation().Generate() with { Role = UserRole.User().Value });
        user.AddPermission(Permission.ManageAgents());

        // Act
        user.UpdateRole(Kept(user, UserRole.Admin()));

        // Assert
        user.Role.ShouldBe(UserRole.Admin());
        user.UserPermissions.Select(p => p.Permission).ShouldBe(UserRole.Admin().GetDefaultPermissions(), ignoreOrder: true);
        user.DomainEvents.ShouldBe([new UserCreated(user.Id), new UserUpdated(user.Id)]);
    }

    [Test]
    public void the_same_role_keeps_the_permissions_and_queues_no_event()
    {
        // Arrange
        var user = User.Create(new FakeUserForCreation().Generate() with { Role = UserRole.User().Value });
        user.AddPermission(Permission.ManageAgents());
        var permissions = user.UserPermissions.ToList();

        // Act
        user.UpdateRole(Kept(user, UserRole.User()));

        // Assert
        user.UserPermissions.ShouldBe(permissions);
        user.DomainEvents.ShouldBe([new UserCreated(user.Id)]);
    }

    [Test]
    public void the_proof_of_another_user_or_of_a_removal_is_refused()
    {
        // Arrange
        var user = User.Create(new FakeUserForCreation().Generate() with { Role = UserRole.User().Value });
        var other = User.Create(new FakeUserForCreation().Generate() with { Role = UserRole.User().Value });
        var removal = ((AdminDecision.Kept)TenantAdmins.Decide(new AdminCase(user, new AdminChange.Removal(), []))).Proof;

        // Act
        var ofOther = () => user.UpdateRole(Kept(other, UserRole.Admin()));
        var ofRemoval = () => user.UpdateRole(removal);

        // Assert
        ofOther.ShouldThrow<ArgumentException>();
        ofRemoval.ShouldThrow<ArgumentException>();
        user.Role.ShouldBe(UserRole.User());
    }

    private static AdminKept Kept(User user, UserRole role)
        => ((AdminDecision.Kept)TenantAdmins.Decide(new AdminCase(user, new AdminChange.NewRole(role),
            user.Role == UserRole.Admin() ? [user.Id, "usr_other"] : []))).Proof;
}
