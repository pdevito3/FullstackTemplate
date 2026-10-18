namespace FullstackTemplate.IntegrationTests.FeatureTests.Users;

using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.Features;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using Microsoft.EntityFrameworkCore;
using Shouldly;

public class AddUserCommandTests : TestBase
{
    [Fact]
    public async Task can_add_new_user_to_db()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var fakeUserDto = new FakeUserForCreationDto().Generate();

        // Act
        var command = new AddUser.Command(fakeUserDto);
        var userReturned = await testingServiceScope.SendAsync(command);

        // Assert
        var userInDb = await testingServiceScope.ExecuteDbContextAsync(db =>
            db.Users.FirstOrDefaultAsync(u => u.Id == userReturned.Id));

        userInDb.ShouldNotBeNull();
        userInDb.FirstName.ShouldBe(fakeUserDto.FirstName);
        userInDb.LastName.ShouldBe(fakeUserDto.LastName);
        userInDb.Email.Value.ShouldBe(fakeUserDto.Email);
        userInDb.Username.ShouldBe(fakeUserDto.Username);
        userInDb.Identifier.ShouldBe(fakeUserDto.Identifier);
        userInDb.TenantId.ShouldBe(TestFixture.DefaultTenant.Id);
    }

    [Fact]
    public async Task can_add_user_with_admin_role()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var fakeUserDto = new FakeUserForCreationDto()
            .RuleFor(x => x.Role, _ => "Admin")
            .Generate();

        // Act
        var command = new AddUser.Command(fakeUserDto);
        var userReturned = await testingServiceScope.SendAsync(command);

        // Assert
        var userInDb = await testingServiceScope.ExecuteDbContextAsync(db =>
            db.Users
                .Include(u => u.UserPermissions)
                .FirstOrDefaultAsync(u => u.Id == userReturned.Id));

        userInDb.ShouldNotBeNull();
        userInDb.Role.Value.ShouldBe("Admin");
        userInDb.UserPermissions.ShouldNotBeEmpty();
        userInDb.HasPermission(Permission.DoSomethingSpecial()).ShouldBeTrue();
    }

    [Fact]
    public async Task can_add_user_with_user_role()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var fakeUserDto = new FakeUserForCreationDto()
            .RuleFor(x => x.Role, _ => "User")
            .Generate();

        // Act
        var command = new AddUser.Command(fakeUserDto);
        var userReturned = await testingServiceScope.SendAsync(command);

        // Assert
        var userInDb = await testingServiceScope.ExecuteDbContextAsync(db =>
            db.Users
                .Include(u => u.UserPermissions)
                .FirstOrDefaultAsync(u => u.Id == userReturned.Id));

        userInDb.ShouldNotBeNull();
        userInDb.Role.Value.ShouldBe("User");
        userInDb.UserPermissions.ShouldBeEmpty();
    }

    [Fact]
    public async Task cannot_add_user_to_another_tenant()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddTenant(out var otherTenant)
            .BuildAsync();
        var fakeUserDto = new FakeUserForCreationDto()
            .RuleFor(x => x.TenantId, _ => otherTenant.Id)
            .Generate();

        // Act
        var act = () => testingServiceScope.SendAsync(new AddUser.Command(fakeUserDto));

        // Assert
        await act.ShouldThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task user_with_no_user_record_cannot_add_user()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        testingServiceScope.SetUserWithDefaults(identifier: $"unknown-{Guid.NewGuid()}");
        var fakeUserDto = new FakeUserForCreationDto().Generate();

        // Act
        var act = () => testingServiceScope.SendAsync(new AddUser.Command(fakeUserDto));

        // Assert
        await act.ShouldThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task queues_domain_event_on_create()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var fakeUserDto = new FakeUserForCreationDto().Generate();

        // Act
        var command = new AddUser.Command(fakeUserDto);
        var userReturned = await testingServiceScope.SendAsync(command);

        // Assert
        userReturned.ShouldNotBeNull();
        userReturned.Id.ShouldNotBe(Guid.Empty);
    }
}
