namespace FullstackTemplate.IntegrationTests.FeatureTests.Users;

using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.Users;
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
        userReturned.Role.ShouldBe("Admin");
        userReturned.Permissions.ShouldNotBeEmpty();
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
