namespace FullstackTemplate.IntegrationTests.FeatureTests.Users;

using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.Dtos;
using FullstackTemplate.Server.Domain.Users.Features;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Shouldly;

public class GetUserQueryTests : TestBase
{
    [Test]
    public async Task can_get_existing_user()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddUser(out var fakeUser, u => { u.FirstName = "John"; u.LastName = "Doe"; })
            .BuildAsync();

        // Act
        var query = new GetUser.Query(fakeUser.Id);
        var userReturned = await testingServiceScope.SendAsync(query);

        // Assert
        userReturned.ShouldNotBeNull();
        userReturned.Id.ShouldBe(fakeUser.Id);
        userReturned.FirstName.ShouldBe("John");
        userReturned.LastName.ShouldBe("Doe");
    }

    [Test]
    public async Task get_user_returns_with_permissions()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddUser(out var fakeUser, u => u.Role = "Admin")
            .BuildAsync();

        // Act
        var query = new GetUser.Query(fakeUser.Id);
        var userReturned = await testingServiceScope.SendAsync(query);

        // Assert
        userReturned.ShouldNotBeNull();
        userReturned.Roles.ShouldBe(["Admin"]);
        userReturned.Permissions.ShouldNotBeEmpty();
    }

    [Test]
    public async Task every_read_returns_the_overrides_of_the_user()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddUser(out var fakeUser, u => u.Role = "User")
            .BuildAsync();
        await testingServiceScope.SendAsync(new AddUserPermission.Command(fakeUser.Id, "manage_tenant"));
        await testingServiceScope.SendAsync(new AddUserPermission.Command(fakeUser.Id, "read_users"));
        await testingServiceScope.SendAsync(new RemoveUserPermission.Command(fakeUser.Id, "read_users"));

        // Act - a new scope, so that each read loads the overrides and does not find them tracked from the writes
        var readScope = new TestingServiceScope();
        var byId = await readScope.SendAsync(new GetUser.Query(fakeUser.Id));
        var byIdentifier = await readScope.SendAsync(new GetUserByIdentifier.Query(fakeUser.Identifier));
        var list = await readScope.SendAsync(new GetUserList.Query(UserParametersDto.From(null, null, $"""id == "{fakeUser.Id}" """, null)));

        // Assert
        UserPermissionOverrideDto[] expected =
        [
            new() { Permission = "manage_tenant", Granted = true },
            new() { Permission = "read_users", Granted = false },
        ];
        byId.Permissions.ShouldBe(["manage_tenant"]);
        byId.PermissionOverrides.OrderBy(o => o.Permission).ShouldBe(expected);
        byIdentifier.PermissionOverrides.OrderBy(o => o.Permission).ShouldBe(expected);
        list.Single(u => u.Id == fakeUser.Id).PermissionOverrides.OrderBy(o => o.Permission).ShouldBe(expected);
    }

    [Test]
    public async Task get_nonexistent_user_throws_not_found()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var nonExistentId = EntityId.New(User.IdPrefix);

        // Act & Assert
        var query = new GetUser.Query(nonExistentId);
        await Should.ThrowAsync<Exception>(async () =>
            await testingServiceScope.SendAsync(query));
    }
}
