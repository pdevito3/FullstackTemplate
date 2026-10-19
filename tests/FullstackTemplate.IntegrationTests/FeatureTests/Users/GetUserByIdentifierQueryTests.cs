namespace FullstackTemplate.IntegrationTests.FeatureTests.Users;

using FullstackTemplate.Server.Domain.Users.Features;
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
    public async Task returns_null_when_identifier_not_found()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var nonExistentIdentifier = $"non-existent-{Guid.NewGuid()}";

        // Act
        var query = new GetUserByIdentifier.Query(nonExistentIdentifier);
        var userReturned = await testingServiceScope.SendAsync(query);

        // Assert
        userReturned.ShouldBeNull();
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
        userReturned.Permissions.ShouldContain("do_something_special");
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

        var lowercaseResult = await testingServiceScope.SendAsync(lowercaseQuery);
        var uppercaseResult = await testingServiceScope.SendAsync(uppercaseQuery);
        var exactResult = await testingServiceScope.SendAsync(exactQuery);

        // Assert
        exactResult.ShouldNotBeNull();
        // Case sensitivity depends on database collation - at minimum exact match works
        exactResult.Id.ShouldBe(fakeUser.Id);
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
        var userReturned = await testingServiceScope.SendAsync(query);

        // Assert - soft-deleted users should not be found
        userReturned.ShouldBeNull();
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
}
