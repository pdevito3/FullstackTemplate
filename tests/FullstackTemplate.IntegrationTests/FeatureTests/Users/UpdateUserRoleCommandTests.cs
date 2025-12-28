namespace FullstackTemplate.IntegrationTests.FeatureTests.Users;

using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.Features;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using Microsoft.EntityFrameworkCore;
using Shouldly;

public class UpdateUserRoleCommandTests : TestBase
{
    [Fact]
    public async Task can_change_role_from_user_to_admin()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var fakeUserDto = new FakeUserForCreationDto()
            .RuleFor(x => x.Role, _ => "User")
            .Generate();
        var createCommand = new AddUser.Command(fakeUserDto);
        var createdUser = await testingServiceScope.SendAsync(createCommand);

        // Act
        var command = new UpdateUserRole.Command(createdUser.Id, "Admin");
        var userReturned = await testingServiceScope.SendAsync(command);

        // Assert
        userReturned.Role.ShouldBe("Admin");
        userReturned.Permissions.ShouldContain("do_something_special");
    }

    [Fact]
    public async Task changing_role_to_admin_grants_admin_permissions_in_db()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var fakeUserDto = new FakeUserForCreationDto()
            .RuleFor(x => x.Role, _ => "User")
            .Generate();
        var createCommand = new AddUser.Command(fakeUserDto);
        var createdUser = await testingServiceScope.SendAsync(createCommand);

        // Act
        var command = new UpdateUserRole.Command(createdUser.Id, "Admin");
        await testingServiceScope.SendAsync(command);

        // Assert
        var userInDb = await testingServiceScope.ExecuteDbContextAsync(db =>
            db.Users
                .Include(u => u.UserPermissions)
                .FirstOrDefaultAsync(u => u.Id == createdUser.Id));

        userInDb.ShouldNotBeNull();
        userInDb.Role.Value.ShouldBe("Admin");
        userInDb.HasPermission(Permission.DoSomethingSpecial()).ShouldBeTrue();
    }

    [Fact]
    public async Task can_change_role_from_admin_to_user()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var fakeUserDto = new FakeUserForCreationDto()
            .RuleFor(x => x.Role, _ => "Admin")
            .Generate();
        var createCommand = new AddUser.Command(fakeUserDto);
        var createdUser = await testingServiceScope.SendAsync(createCommand);

        // Act
        var command = new UpdateUserRole.Command(createdUser.Id, "User");
        var userReturned = await testingServiceScope.SendAsync(command);

        // Assert
        userReturned.Role.ShouldBe("User");
        userReturned.Permissions.ShouldBeEmpty();
    }

    [Fact]
    public async Task changing_role_from_admin_to_user_removes_permissions()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var fakeUserDto = new FakeUserForCreationDto()
            .RuleFor(x => x.Role, _ => "Admin")
            .Generate();
        var createCommand = new AddUser.Command(fakeUserDto);
        var createdUser = await testingServiceScope.SendAsync(createCommand);

        // Verify admin has permissions before role change
        createdUser.Permissions.ShouldNotBeEmpty();

        // Act
        var command = new UpdateUserRole.Command(createdUser.Id, "User");
        await testingServiceScope.SendAsync(command);

        // Assert
        var userAfter = await testingServiceScope.ExecuteDbContextAsync(db =>
            db.Users
                .Include(u => u.UserPermissions)
                .FirstOrDefaultAsync(u => u.Id == createdUser.Id));

        userAfter.ShouldNotBeNull();
        userAfter.Role.Value.ShouldBe("User");
        userAfter.UserPermissions.ShouldBeEmpty();
    }

    [Fact]
    public async Task changing_role_resets_custom_permissions_to_role_defaults()
    {
        // Arrange - Create admin
        var testingServiceScope = new TestingServiceScope();
        var fakeUserDto = new FakeUserForCreationDto()
            .RuleFor(x => x.Role, _ => "Admin")
            .Generate();
        var createCommand = new AddUser.Command(fakeUserDto);
        var createdUser = await testingServiceScope.SendAsync(createCommand);

        // Act - Change to admin again (simulating permission reset)
        var command = new UpdateUserRole.Command(createdUser.Id, "Admin");
        var userReturned = await testingServiceScope.SendAsync(command);

        // Assert - Should have exactly the default admin permissions
        userReturned.Permissions.Count.ShouldBe(1);
        userReturned.Permissions.ShouldContain("do_something_special");
    }

    [Fact]
    public async Task throws_when_user_not_found()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        var command = new UpdateUserRole.Command(nonExistentId, "Admin");
        await Should.ThrowAsync<Exception>(async () =>
            await testingServiceScope.SendAsync(command));
    }

    [Fact]
    public async Task throws_when_invalid_role()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var fakeUserDto = new FakeUserForCreationDto().Generate();
        var createCommand = new AddUser.Command(fakeUserDto);
        var createdUser = await testingServiceScope.SendAsync(createCommand);

        // Act & Assert
        var command = new UpdateUserRole.Command(createdUser.Id, "InvalidRole");
        await Should.ThrowAsync<ValidationException>(async () =>
            await testingServiceScope.SendAsync(command));
    }

    [Fact]
    public async Task setting_same_role_resets_permissions_to_default()
    {
        // Arrange - Create admin user and remove permission
        var testingServiceScope = new TestingServiceScope();
        var fakeUserDto = new FakeUserForCreationDto()
            .RuleFor(x => x.Role, _ => "Admin")
            .Generate();
        var createCommand = new AddUser.Command(fakeUserDto);
        var createdUser = await testingServiceScope.SendAsync(createCommand);

        // Remove permission via command
        await testingServiceScope.SendAsync(
            new RemoveUserPermission.Command(createdUser.Id, "do_something_special"));

        // Verify permission was removed
        var userAfterRemove = await testingServiceScope.SendAsync(new GetUser.Query(createdUser.Id));
        userAfterRemove.Permissions.ShouldBeEmpty();

        // Act - Set to Admin again
        var command = new UpdateUserRole.Command(createdUser.Id, "Admin");
        var userReturned = await testingServiceScope.SendAsync(command);

        // Assert - Permission should be restored
        userReturned.Permissions.ShouldContain("do_something_special");
    }
}
