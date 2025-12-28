namespace FullstackTemplate.IntegrationTests.FeatureTests.Users;

using FullstackTemplate.Server.Domain.Users.Features;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using Shouldly;

public class GetUserQueryTests : TestBase
{
    [Fact]
    public async Task can_get_existing_user()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var fakeUser = new FakeUserBuilder()
            .WithFirstName("John")
            .WithLastName("Doe")
            .Build();
        await testingServiceScope.InsertAsync(fakeUser);

        // Act
        var query = new GetUser.Query(fakeUser.Id);
        var userReturned = await testingServiceScope.SendAsync(query);

        // Assert
        userReturned.ShouldNotBeNull();
        userReturned.Id.ShouldBe(fakeUser.Id);
        userReturned.FirstName.ShouldBe("John");
        userReturned.LastName.ShouldBe("Doe");
    }

    [Fact]
    public async Task get_user_returns_with_permissions()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var fakeUser = new FakeUserBuilder()
            .WithAdminRole()
            .Build();
        await testingServiceScope.InsertAsync(fakeUser);

        // Act
        var query = new GetUser.Query(fakeUser.Id);
        var userReturned = await testingServiceScope.SendAsync(query);

        // Assert
        userReturned.ShouldNotBeNull();
        userReturned.Role.ShouldBe("Admin");
        userReturned.Permissions.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task get_nonexistent_user_throws_not_found()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        var query = new GetUser.Query(nonExistentId);
        await Should.ThrowAsync<Exception>(async () =>
            await testingServiceScope.SendAsync(query));
    }
}
