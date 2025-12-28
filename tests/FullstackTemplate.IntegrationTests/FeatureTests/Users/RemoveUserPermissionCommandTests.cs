namespace FullstackTemplate.IntegrationTests.FeatureTests.Users;

using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.Features;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using Microsoft.EntityFrameworkCore;
using Shouldly;

public class RemoveUserPermissionCommandTests : TestBase
{
    [Fact]
    public async Task can_remove_permission_from_user()
    {
        // Arrange - Create admin user via command
        var testingServiceScope = new TestingServiceScope();
        var fakeUserDto = new FakeUserForCreationDto()
            .RuleFor(x => x.Role, _ => "Admin")
            .Generate();
        var createCommand = new AddUser.Command(fakeUserDto);
        var createdUser = await testingServiceScope.SendAsync(createCommand);

        // Verify permission exists first
        createdUser.Permissions.ShouldContain("do_something_special");

        // Act
        var command = new RemoveUserPermission.Command(createdUser.Id, "do_something_special");
        var userReturned = await testingServiceScope.SendAsync(command);

        // Assert
        userReturned.Permissions.ShouldNotContain("do_something_special");
    }

    [Fact]
    public async Task permission_is_removed_from_database()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var fakeUserDto = new FakeUserForCreationDto()
            .RuleFor(x => x.Role, _ => "Admin")
            .Generate();
        var createCommand = new AddUser.Command(fakeUserDto);
        var createdUser = await testingServiceScope.SendAsync(createCommand);

        // Act
        var command = new RemoveUserPermission.Command(createdUser.Id, "do_something_special");
        await testingServiceScope.SendAsync(command);

        // Assert
        var userInDb = await testingServiceScope.ExecuteDbContextAsync(db =>
            db.Users
                .Include(u => u.UserPermissions)
                .FirstOrDefaultAsync(u => u.Id == createdUser.Id));

        userInDb.ShouldNotBeNull();
        userInDb.HasPermission(Permission.DoSomethingSpecial()).ShouldBeFalse();
        userInDb.UserPermissions.ShouldBeEmpty();
    }

    [Fact]
    public async Task removing_nonexistent_permission_is_safe()
    {
        // Arrange - User role has no permissions
        var testingServiceScope = new TestingServiceScope();
        var fakeUserDto = new FakeUserForCreationDto()
            .RuleFor(x => x.Role, _ => "User")
            .Generate();
        var createCommand = new AddUser.Command(fakeUserDto);
        var createdUser = await testingServiceScope.SendAsync(createCommand);

        // Act - Try to remove a permission that doesn't exist
        var command = new RemoveUserPermission.Command(createdUser.Id, "do_something_special");
        var userReturned = await testingServiceScope.SendAsync(command);

        // Assert - Should succeed without error
        userReturned.ShouldNotBeNull();
        userReturned.Permissions.ShouldBeEmpty();
    }

    [Fact]
    public async Task throws_when_user_not_found()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        var command = new RemoveUserPermission.Command(nonExistentId, "do_something_special");
        await Should.ThrowAsync<Exception>(async () =>
            await testingServiceScope.SendAsync(command));
    }

    [Fact]
    public async Task throws_when_invalid_permission()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var fakeUserDto = new FakeUserForCreationDto().Generate();
        var createCommand = new AddUser.Command(fakeUserDto);
        var createdUser = await testingServiceScope.SendAsync(createCommand);

        // Act & Assert
        var command = new RemoveUserPermission.Command(createdUser.Id, "invalid_permission");
        await Should.ThrowAsync<ValidationException>(async () =>
            await testingServiceScope.SendAsync(command));
    }

    [Fact]
    public async Task can_remove_and_verify_via_get()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var fakeUserDto = new FakeUserForCreationDto()
            .RuleFor(x => x.Role, _ => "Admin")
            .Generate();
        var createCommand = new AddUser.Command(fakeUserDto);
        var createdUser = await testingServiceScope.SendAsync(createCommand);

        // Act
        var removeCommand = new RemoveUserPermission.Command(createdUser.Id, "do_something_special");
        await testingServiceScope.SendAsync(removeCommand);

        // Verify via GetUser
        var getQuery = new GetUser.Query(createdUser.Id);
        var userFromGet = await testingServiceScope.SendAsync(getQuery);

        // Assert
        userFromGet.Permissions.ShouldNotContain("do_something_special");
    }

    [Fact]
    public async Task role_remains_unchanged_after_permission_removal()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var fakeUserDto = new FakeUserForCreationDto()
            .RuleFor(x => x.Role, _ => "Admin")
            .Generate();
        var createCommand = new AddUser.Command(fakeUserDto);
        var createdUser = await testingServiceScope.SendAsync(createCommand);

        // Act
        var command = new RemoveUserPermission.Command(createdUser.Id, "do_something_special");
        var userReturned = await testingServiceScope.SendAsync(command);

        // Assert - Role should remain Admin even though permissions were removed
        userReturned.Role.ShouldBe("Admin");
    }

    [Fact]
    public async Task add_then_remove_permission_roundtrip()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var fakeUserDto = new FakeUserForCreationDto()
            .RuleFor(x => x.Role, _ => "User")
            .Generate();
        var createCommand = new AddUser.Command(fakeUserDto);
        var createdUser = await testingServiceScope.SendAsync(createCommand);

        // Act - Add permission
        var addCommand = new AddUserPermission.Command(createdUser.Id, "do_something_special");
        var afterAdd = await testingServiceScope.SendAsync(addCommand);
        afterAdd.Permissions.ShouldContain("do_something_special");

        // Act - Remove permission
        var removeCommand = new RemoveUserPermission.Command(createdUser.Id, "do_something_special");
        var afterRemove = await testingServiceScope.SendAsync(removeCommand);

        // Assert
        afterRemove.Permissions.ShouldBeEmpty();
    }
}
