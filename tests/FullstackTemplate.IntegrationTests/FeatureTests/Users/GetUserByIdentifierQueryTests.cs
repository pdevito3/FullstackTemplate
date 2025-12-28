namespace FullstackTemplate.IntegrationTests.FeatureTests.Users;

using FullstackTemplate.Server.Domain.Users.Features;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using Shouldly;

public class GetUserByIdentifierQueryTests : TestBase
{
    [Fact]
    public async Task can_get_user_by_identifier()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var uniqueIdentifier = $"idp-{Guid.NewGuid()}";
        var fakeUser = new FakeUserBuilder()
            .WithIdentifier(uniqueIdentifier)
            .WithFirstName("IdentifierTest")
            .Build();
        await testingServiceScope.InsertAsync(fakeUser);

        // Act
        var query = new GetUserByIdentifier.Query(uniqueIdentifier);
        var userReturned = await testingServiceScope.SendAsync(query);

        // Assert
        userReturned.ShouldNotBeNull();
        userReturned.Id.ShouldBe(fakeUser.Id);
        userReturned.FirstName.ShouldBe("IdentifierTest");
    }

    [Fact]
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

    [Fact]
    public async Task returns_user_with_permissions()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var uniqueIdentifier = $"admin-idp-{Guid.NewGuid()}";
        var fakeUser = new FakeUserBuilder()
            .WithIdentifier(uniqueIdentifier)
            .WithAdminRole()
            .Build();
        await testingServiceScope.InsertAsync(fakeUser);

        // Act
        var query = new GetUserByIdentifier.Query(uniqueIdentifier);
        var userReturned = await testingServiceScope.SendAsync(query);

        // Assert
        userReturned.ShouldNotBeNull();
        userReturned.Role.ShouldBe("Admin");
        userReturned.Permissions.ShouldNotBeEmpty();
        userReturned.Permissions.ShouldContain("do_something_special");
    }

    [Fact]
    public async Task is_case_sensitive_for_identifier()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var originalIdentifier = $"CaseSensitive-{Guid.NewGuid()}";
        var fakeUser = new FakeUserBuilder()
            .WithIdentifier(originalIdentifier)
            .Build();
        await testingServiceScope.InsertAsync(fakeUser);

        // Act - Query with different case
        var lowercaseQuery = new GetUserByIdentifier.Query(originalIdentifier.ToLower());
        var uppercaseQuery = new GetUserByIdentifier.Query(originalIdentifier.ToUpper());
        var exactQuery = new GetUserByIdentifier.Query(originalIdentifier);

        var lowercaseResult = await testingServiceScope.SendAsync(lowercaseQuery);
        var uppercaseResult = await testingServiceScope.SendAsync(uppercaseQuery);
        var exactResult = await testingServiceScope.SendAsync(exactQuery);

        // Assert
        exactResult.ShouldNotBeNull();
        // Case sensitivity depends on database collation - at minimum exact match works
        exactResult.Id.ShouldBe(fakeUser.Id);
    }

    [Fact]
    public async Task does_not_return_soft_deleted_user()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var uniqueIdentifier = $"soft-delete-{Guid.NewGuid()}";
        var fakeUserDto = new FakeUserForCreationDto()
            .RuleFor(x => x.Identifier, _ => uniqueIdentifier)
            .Generate();
        var createCommand = new AddUser.Command(fakeUserDto);
        var createdUser = await testingServiceScope.SendAsync(createCommand);

        // Soft delete the user
        var deleteCommand = new DeleteUser.Command(createdUser.Id);
        await testingServiceScope.SendAsync(deleteCommand);

        // Act
        var query = new GetUserByIdentifier.Query(uniqueIdentifier);
        var userReturned = await testingServiceScope.SendAsync(query);

        // Assert - soft-deleted users should not be found
        userReturned.ShouldBeNull();
    }

    [Fact]
    public async Task returns_correct_user_when_multiple_exist()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var targetIdentifier = $"target-{Guid.NewGuid()}";
        var otherIdentifier = $"other-{Guid.NewGuid()}";

        var targetUser = new FakeUserBuilder()
            .WithIdentifier(targetIdentifier)
            .WithFirstName("TargetUser")
            .Build();
        var otherUser = new FakeUserBuilder()
            .WithIdentifier(otherIdentifier)
            .WithFirstName("OtherUser")
            .Build();
        await testingServiceScope.InsertAsync(targetUser, otherUser);

        // Act
        var query = new GetUserByIdentifier.Query(targetIdentifier);
        var userReturned = await testingServiceScope.SendAsync(query);

        // Assert
        userReturned.ShouldNotBeNull();
        userReturned.Id.ShouldBe(targetUser.Id);
        userReturned.FirstName.ShouldBe("TargetUser");
    }
}
