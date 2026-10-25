namespace FullstackTemplate.IntegrationTests.FeatureTests.Users;

using FullstackTemplate.Server.Domain.Users.Features;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Shouldly;

public class GetUserByIdentifierQueryTests : TestBase
{
    [Test]
    public async Task can_get_user_by_identifier()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var uniqueIdentifier = $"idp-{Guid.NewGuid()}";
        await testingServiceScope.StartGraph(out _)
            .AddUser(out var fakeUser, u => { u.Identifier = uniqueIdentifier; u.FirstName = "IdentifierTest"; })
            .BuildAsync();

        // Act
        var query = new GetUserByIdentifier.Query(uniqueIdentifier);
        var userReturned = await testingServiceScope.SendAsync(query);

        // Assert
        userReturned.ShouldNotBeNull();
        userReturned.Id.ShouldBe(fakeUser.Id);
        userReturned.FirstName.ShouldBe("IdentifierTest");
    }

    [Test]
    public async Task returns_the_live_user_when_a_deleted_user_has_the_identifier()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var identifier = $"signed-up-again-{Guid.NewGuid()}";
        await testingServiceScope.StartGraph(out _)
            .AddUser(out var deleted, u => u.Identifier = identifier)
            .BuildAsync();
        await testingServiceScope.SendAsync(new DeleteUser.Command(deleted.Id));
        await testingServiceScope.StartGraph(out _)
            .AddUser(out var live, u => u.Identifier = identifier)
            .BuildAsync();

        // Act
        var userReturned = await testingServiceScope.SendAsync(new GetUserByIdentifier.Query(identifier));

        // Assert
        userReturned.Id.ShouldBe(live.Id);
    }

    [Test]
    public async Task throws_not_found_when_identifier_not_found()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var nonExistentIdentifier = $"non-existent-{Guid.NewGuid()}";

        // Act
        var query = new GetUserByIdentifier.Query(nonExistentIdentifier);
        var act = () => testingServiceScope.SendAsync(query);

        // Assert
        await act.ShouldThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task returns_user_with_permissions()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var uniqueIdentifier = $"admin-idp-{Guid.NewGuid()}";
        await testingServiceScope.StartGraph(out _)
            .AddUser(out var fakeUser, u => { u.Identifier = uniqueIdentifier; u.Role = "Admin"; })
            .BuildAsync();

        // Act
        var query = new GetUserByIdentifier.Query(uniqueIdentifier);
        var userReturned = await testingServiceScope.SendAsync(query);

        // Assert
        userReturned.ShouldNotBeNull();
        userReturned.Role.ShouldBe("Admin");
        userReturned.Permissions.ShouldNotBeEmpty();
        userReturned.Permissions.ShouldContain("manage_tenant");
    }

    [Test]
    public async Task is_case_sensitive_for_identifier()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var originalIdentifier = $"CaseSensitive-{Guid.NewGuid()}";
        await testingServiceScope.StartGraph(out _)
            .AddUser(out var fakeUser, u => u.Identifier = originalIdentifier)
            .BuildAsync();

        // Act - Query with different case
        var lowercaseQuery = new GetUserByIdentifier.Query(originalIdentifier.ToLowerInvariant());
        var uppercaseQuery = new GetUserByIdentifier.Query(originalIdentifier.ToUpperInvariant());
        var exactQuery = new GetUserByIdentifier.Query(originalIdentifier);

        var lowercase = () => testingServiceScope.SendAsync(lowercaseQuery);
        var uppercase = () => testingServiceScope.SendAsync(uppercaseQuery);
        var exactResult = await testingServiceScope.SendAsync(exactQuery);

        // Assert
        exactResult.Id.ShouldBe(fakeUser.Id);
        // The default Postgres collation compares with case
        await lowercase.ShouldThrowAsync<NotFoundException>();
        await uppercase.ShouldThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task does_not_return_soft_deleted_user()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var uniqueIdentifier = $"soft-delete-{Guid.NewGuid()}";
        await testingServiceScope.StartGraph(out _)
            .AddUser(out var createdUser, u => u.Identifier = uniqueIdentifier)
            .BuildAsync();

        // Soft delete the user
        var deleteCommand = new DeleteUser.Command(createdUser.Id);
        await testingServiceScope.SendAsync(deleteCommand);

        // Act
        var query = new GetUserByIdentifier.Query(uniqueIdentifier);
        var act = () => testingServiceScope.SendAsync(query);

        // Assert - soft-deleted users should not be found
        await act.ShouldThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task returns_correct_user_when_multiple_exist()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var targetIdentifier = $"target-{Guid.NewGuid()}";
        var otherIdentifier = $"other-{Guid.NewGuid()}";

        await testingServiceScope.StartGraph(out _)
            .AddUsers(out var users,
                u => { u.Identifier = targetIdentifier; u.FirstName = "TargetUser"; },
                u => { u.Identifier = otherIdentifier; u.FirstName = "OtherUser"; })
            .BuildAsync();
        var targetUser = users[0];

        // Act
        var query = new GetUserByIdentifier.Query(targetIdentifier);
        var userReturned = await testingServiceScope.SendAsync(query);

        // Assert
        userReturned.ShouldNotBeNull();
        userReturned.Id.ShouldBe(targetUser.Id);
        userReturned.FirstName.ShouldBe("TargetUser");
    }

    [Test]
    public async Task response_has_the_permissions_that_get_user_returns()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var identifier = $"with-permissions-{Guid.NewGuid()}";
        await testingServiceScope.StartGraph(out _)
            .AddUser(out var createdUser, u => { u.Identifier = identifier; u.Role = "Admin"; })
            .BuildAsync();

        // Act
        var userReturned = await testingServiceScope.SendAsync(new GetUserByIdentifier.Query(identifier));

        // Assert
        var userFromGet = await new TestingServiceScope().SendAsync(new GetUser.Query(createdUser.Id));
        userFromGet.Permissions.ShouldNotBeEmpty();
        userReturned.Permissions.ShouldBe(userFromGet.Permissions, ignoreOrder: true);
    }
}
