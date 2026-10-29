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
        user.UpdateRole(UserRole.Admin());

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
        user.UpdateRole(UserRole.User());

        // Assert
        user.UserPermissions.ShouldBe(permissions);
        user.DomainEvents.ShouldBe([new UserCreated(user.Id)]);
    }
}
