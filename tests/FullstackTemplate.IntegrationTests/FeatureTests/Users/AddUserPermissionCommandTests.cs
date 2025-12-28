namespace FullstackTemplate.IntegrationTests.FeatureTests.Users;

using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.Features;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using Microsoft.EntityFrameworkCore;
using Shouldly;

public class AddUserPermissionCommandTests : TestBase
{
    [Fact]
    public async Task can_add_permission_to_user()
    {
        // Arrange - Use AddUser command to create user (avoids EF tracking issues)
        var testingServiceScope = new TestingServiceScope();
        var fakeUserDto = new FakeUserForCreationDto()
            .RuleFor(x => x.Role, _ => "User")
            .Generate();
        var createCommand = new AddUser.Command(fakeUserDto);
        var createdUser = await testingServiceScope.SendAsync(createCommand);

        // Act
        var command = new AddUserPermission.Command(createdUser.Id, "do_something_special");
        var userReturned = await testingServiceScope.SendAsync(command);

        // Assert
        userReturned.ShouldNotBeNull();
        userReturned.Permissions.ShouldContain("do_something_special");
    }

    [Fact]
    public async Task permission_is_persisted_to_database()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var fakeUserDto = new FakeUserForCreationDto()
            .RuleFor(x => x.Role, _ => "User")
            .Generate();
        var createCommand = new AddUser.Command(fakeUserDto);
        var createdUser = await testingServiceScope.SendAsync(createCommand);

        // Act
        var command = new AddUserPermission.Command(createdUser.Id, "do_something_special");
        await testingServiceScope.SendAsync(command);

        // Assert
        var userInDb = await testingServiceScope.ExecuteDbContextAsync(db =>
            db.Users
                .Include(u => u.UserPermissions)
                .FirstOrDefaultAsync(u => u.Id == createdUser.Id));

        userInDb.ShouldNotBeNull();
        userInDb.HasPermission(Permission.DoSomethingSpecial()).ShouldBeTrue();
    }

    [Fact]
    public async Task adding_duplicate_permission_is_idempotent()
    {
        // Arrange - Admin already has do_something_special by default
        var testingServiceScope = new TestingServiceScope();
        var fakeUserDto = new FakeUserForCreationDto()
            .RuleFor(x => x.Role, _ => "Admin")
            .Generate();
        var createCommand = new AddUser.Command(fakeUserDto);
        var createdUser = await testingServiceScope.SendAsync(createCommand);

        // Act - Add the same permission again
        var command = new AddUserPermission.Command(createdUser.Id, "do_something_special");
        var userReturned = await testingServiceScope.SendAsync(command);

        // Assert - Should still have exactly one instance of the permission
        var userInDb = await testingServiceScope.ExecuteDbContextAsync(db =>
            db.Users
                .Include(u => u.UserPermissions)
                .FirstOrDefaultAsync(u => u.Id == createdUser.Id));

        userInDb.ShouldNotBeNull();
        userInDb.UserPermissions
            .Count(p => p.Permission.Value == "do_something_special")
            .ShouldBe(1);
    }

    [Fact]
    public async Task throws_when_user_not_found()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        var command = new AddUserPermission.Command(nonExistentId, "do_something_special");
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
        var command = new AddUserPermission.Command(createdUser.Id, "invalid_permission");
        await Should.ThrowAsync<ValidationException>(async () =>
            await testingServiceScope.SendAsync(command));
    }

    [Fact]
    public async Task returns_all_permissions_after_adding()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var fakeUserDto = new FakeUserForCreationDto()
            .RuleFor(x => x.Role, _ => "User")
            .Generate();
        var createCommand = new AddUser.Command(fakeUserDto);
        var createdUser = await testingServiceScope.SendAsync(createCommand);

        // Act
        var command = new AddUserPermission.Command(createdUser.Id, "do_something_special");
        var userReturned = await testingServiceScope.SendAsync(command);

        // Assert
        userReturned.Permissions.Count.ShouldBe(1);
        userReturned.Permissions.ShouldContain("do_something_special");
    }

    [Fact]
    public async Task can_add_permission_and_verify_via_get()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var fakeUserDto = new FakeUserForCreationDto()
            .RuleFor(x => x.Role, _ => "User")
            .Generate();
        var createCommand = new AddUser.Command(fakeUserDto);
        var createdUser = await testingServiceScope.SendAsync(createCommand);

        // Act
        var addCommand = new AddUserPermission.Command(createdUser.Id, "do_something_special");
        await testingServiceScope.SendAsync(addCommand);

        // Verify via GetUser
        var getQuery = new GetUser.Query(createdUser.Id);
        var userFromGet = await testingServiceScope.SendAsync(getQuery);

        // Assert
        userFromGet.Permissions.ShouldContain("do_something_special");
    }
}
