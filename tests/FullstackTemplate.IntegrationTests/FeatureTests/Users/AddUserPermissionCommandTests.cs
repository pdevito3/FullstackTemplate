namespace FullstackTemplate.IntegrationTests.FeatureTests.Users;

using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.Features;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Microsoft.EntityFrameworkCore;
using Shouldly;

public class AddUserPermissionCommandTests : TestBase
{
    [Test]
    public async Task can_add_permission_to_user()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddUser(out var createdUser, u => u.Role = "User")
            .BuildAsync();

        // Act
        var command = new AddUserPermission.Command(createdUser.Id, "manage_tenant");
        var userReturned = await testingServiceScope.SendAsync(command);

        // Assert
        userReturned.ShouldNotBeNull();
        userReturned.Permissions.ShouldContain("manage_tenant");
    }

    [Test]
    public async Task permission_is_persisted_to_database()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddUser(out var createdUser, u => u.Role = "User")
            .BuildAsync();

        // Act
        var command = new AddUserPermission.Command(createdUser.Id, "manage_tenant");
        await testingServiceScope.SendAsync(command);

        // Assert
        var userInDb = await testingServiceScope.ExecuteDbContextAsync(db =>
            db.Users
                .Include(u => u.UserPermissions)
                .FirstOrDefaultAsync(u => u.Id == createdUser.Id));

        userInDb.ShouldNotBeNull();
        userInDb.HasPermission(Permission.ManageTenant()).ShouldBeTrue();
    }

    [Test]
    public async Task adding_duplicate_permission_is_idempotent()
    {
        // Arrange - Admin already has manage_tenant by default
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddUser(out var createdUser, u => u.Role = "Admin")
            .BuildAsync();

        // Act - Add the same permission again
        var command = new AddUserPermission.Command(createdUser.Id, "manage_tenant");
        var userReturned = await testingServiceScope.SendAsync(command);

        // Assert - Should still have exactly one instance of the permission
        var userInDb = await testingServiceScope.ExecuteDbContextAsync(db =>
            db.Users
                .Include(u => u.UserPermissions)
                .FirstOrDefaultAsync(u => u.Id == createdUser.Id));

        userInDb.ShouldNotBeNull();
        userInDb.UserPermissions
            .Count(p => p.Permission.Value == "manage_tenant")
            .ShouldBe(1);
    }

    [Test]
    public async Task throws_when_user_not_found()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var nonExistentId = EntityId.New(User.IdPrefix);

        // Act & Assert
        var command = new AddUserPermission.Command(nonExistentId, "manage_tenant");
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
        var command = new AddUserPermission.Command(createdUser.Id, "invalid_permission");
        await Should.ThrowAsync<ValidationException>(async () =>
            await testingServiceScope.SendAsync(command));
    }

    [Test]
    public async Task returns_all_permissions_after_adding()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddUser(out var createdUser, u => u.Role = "User")
            .BuildAsync();

        // Act
        var command = new AddUserPermission.Command(createdUser.Id, "manage_tenant");
        var userReturned = await testingServiceScope.SendAsync(command);

        // Assert
        userReturned.Permissions.Count.ShouldBe(1);
        userReturned.Permissions.ShouldContain("manage_tenant");
    }

    [Test]
    public async Task can_add_permission_and_verify_via_get()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddUser(out var createdUser, u => u.Role = "User")
            .BuildAsync();

        // Act
        var addCommand = new AddUserPermission.Command(createdUser.Id, "manage_tenant");
        await testingServiceScope.SendAsync(addCommand);

        // Verify via GetUser
        var getQuery = new GetUser.Query(createdUser.Id);
        var userFromGet = await testingServiceScope.SendAsync(getQuery);

        // Assert
        userFromGet.Permissions.ShouldContain("manage_tenant");
    }
}
