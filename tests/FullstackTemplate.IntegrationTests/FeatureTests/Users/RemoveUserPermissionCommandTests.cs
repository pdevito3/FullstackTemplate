namespace FullstackTemplate.IntegrationTests.FeatureTests.Users;

using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.Features;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Microsoft.EntityFrameworkCore;
using Shouldly;

public class RemoveUserPermissionCommandTests : TestBase
{
    [Test]
    public async Task can_remove_permission_from_user()
    {
        // Arrange - Create admin user
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddUser(out var createdUser, u => u.Role = "Admin")
            .BuildAsync();

        // Verify permission exists first
        createdUser.HasPermission(Permission.ManageTenant()).ShouldBeTrue();

        // Act
        var command = new RemoveUserPermission.Command(createdUser.Id, "manage_tenant");
        var userReturned = await testingServiceScope.SendAsync(command);

        // Assert
        userReturned.Permissions.ShouldNotContain("manage_tenant");
    }

    [Test]
    public async Task permission_is_removed_from_database()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddUser(out var createdUser, u => u.Role = "Admin")
            .BuildAsync();

        // Act
        var command = new RemoveUserPermission.Command(createdUser.Id, "manage_tenant");
        await testingServiceScope.SendAsync(command);

        // Assert
        var userInDb = await testingServiceScope.ExecuteDbContextAsync(db =>
            db.Users
                .Include(u => u.UserPermissions)
                .FirstOrDefaultAsync(u => u.Id == createdUser.Id));

        userInDb.ShouldNotBeNull();
        userInDb.HasPermission(Permission.ManageTenant()).ShouldBeFalse();
        userInDb.UserPermissions.Select(p => p.Permission.Value)
            .ShouldBe(Permission.ListNames().Except(["manage_tenant"]), ignoreOrder: true);
    }

    [Test]
    public async Task removing_nonexistent_permission_is_safe()
    {
        // Arrange - User role has no permissions
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddUser(out var createdUser, u => u.Role = "User")
            .BuildAsync();

        // Act - Try to remove a permission that doesn't exist
        var command = new RemoveUserPermission.Command(createdUser.Id, "manage_tenant");
        var userReturned = await testingServiceScope.SendAsync(command);

        // Assert - Should succeed without error
        userReturned.ShouldNotBeNull();
        userReturned.Permissions.ShouldBeEmpty();
    }

    [Test]
    public async Task throws_when_user_not_found()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var nonExistentId = EntityId.New(User.IdPrefix);

        // Act & Assert
        var command = new RemoveUserPermission.Command(nonExistentId, "manage_tenant");
        await Should.ThrowAsync<Exception>(async () =>
            await testingServiceScope.SendAsync(command));
    }

    [Test]
    public async Task throws_when_invalid_permission()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddUser(out var createdUser)
            .BuildAsync();

        // Act & Assert
        var command = new RemoveUserPermission.Command(createdUser.Id, "invalid_permission");
        await Should.ThrowAsync<ValidationException>(async () =>
            await testingServiceScope.SendAsync(command));
    }

    [Test]
    public async Task can_remove_and_verify_via_get()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddUser(out var createdUser, u => u.Role = "Admin")
            .BuildAsync();

        // Act
        var removeCommand = new RemoveUserPermission.Command(createdUser.Id, "manage_tenant");
        await testingServiceScope.SendAsync(removeCommand);

        // Verify via GetUser
        var getQuery = new GetUser.Query(createdUser.Id);
        var userFromGet = await testingServiceScope.SendAsync(getQuery);

        // Assert
        userFromGet.Permissions.ShouldNotContain("manage_tenant");
    }

    [Test]
    public async Task role_remains_unchanged_after_permission_removal()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddUser(out var createdUser, u => u.Role = "Admin")
            .BuildAsync();

        // Act
        var command = new RemoveUserPermission.Command(createdUser.Id, "manage_tenant");
        var userReturned = await testingServiceScope.SendAsync(command);

        // Assert - Role should remain Admin even though permissions were removed
        userReturned.Role.ShouldBe("Admin");
    }

    [Test]
    public async Task add_then_remove_permission_roundtrip()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddUser(out var createdUser, u => u.Role = "User")
            .BuildAsync();

        // Act - Add permission
        var addCommand = new AddUserPermission.Command(createdUser.Id, "manage_tenant");
        var afterAdd = await testingServiceScope.SendAsync(addCommand);
        afterAdd.Permissions.ShouldContain("manage_tenant");

        // Act - Remove permission
        var removeCommand = new RemoveUserPermission.Command(createdUser.Id, "manage_tenant");
        var afterRemove = await testingServiceScope.SendAsync(removeCommand);

        // Assert
        afterRemove.Permissions.ShouldBeEmpty();
    }

    [Test]
    public async Task response_has_the_permissions_that_get_user_returns()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddUser(out var createdUser, u => u.Role = "Admin")
            .BuildAsync();

        // Act
        var userReturned = await testingServiceScope.SendAsync(
            new RemoveUserPermission.Command(createdUser.Id, Permission.Names.ManageTenant));

        // Assert
        var userFromGet = await new TestingServiceScope().SendAsync(new GetUser.Query(createdUser.Id));
        userFromGet.Permissions.ShouldNotBeEmpty();
        userReturned.Permissions.ShouldBe(userFromGet.Permissions, ignoreOrder: true);
    }
}
