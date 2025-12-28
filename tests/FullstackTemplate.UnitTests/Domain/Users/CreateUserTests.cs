namespace FullstackTemplate.UnitTests.Domain.Users;

using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.DomainEvents;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using Shouldly;

public class CreateUserTests
{
    [Fact]
    public void can_create_valid_user()
    {
        // Arrange
        var userForCreation = new FakeUserForCreation().Generate();

        // Act
        var user = User.Create(userForCreation);

        // Assert
        user.FirstName.ShouldBe(userForCreation.FirstName);
        user.LastName.ShouldBe(userForCreation.LastName);
        user.Identifier.ShouldBe(userForCreation.Identifier);
        user.Email.Value.ShouldBe(userForCreation.Email);
        user.Username.ShouldBe(userForCreation.Username);
    }

    [Fact]
    public void user_creation_queues_domain_event()
    {
        // Arrange
        var userForCreation = new FakeUserForCreation().Generate();

        // Act
        var user = User.Create(userForCreation);

        // Assert
        user.DomainEvents.Count.ShouldBe(1);
        user.DomainEvents.First().ShouldBeOfType<UserCreated>();
    }

    [Fact]
    public void admin_user_gets_default_permissions()
    {
        // Arrange
        var userForCreation = new FakeUserForCreation()
            .RuleFor(x => x.Role, _ => "Admin")
            .Generate();

        // Act
        var user = User.Create(userForCreation);

        // Assert
        user.Role.Value.ShouldBe("Admin");
        user.UserPermissions.ShouldNotBeEmpty();
        user.HasPermission(Permission.DoSomethingSpecial()).ShouldBeTrue();
    }

    [Fact]
    public void regular_user_gets_no_default_permissions()
    {
        // Arrange
        var userForCreation = new FakeUserForCreation()
            .RuleFor(x => x.Role, _ => "User")
            .Generate();

        // Act
        var user = User.Create(userForCreation);

        // Assert
        user.Role.Value.ShouldBe("User");
        user.UserPermissions.ShouldBeEmpty();
    }

    [Fact]
    public void user_full_name_returns_concatenated_name()
    {
        // Arrange
        var userForCreation = new FakeUserForCreation()
            .RuleFor(x => x.FirstName, _ => "John")
            .RuleFor(x => x.LastName, _ => "Doe")
            .Generate();

        // Act
        var user = User.Create(userForCreation);

        // Assert
        user.FullName.ShouldBe("John Doe");
    }

    [Fact]
    public void missing_first_name_throws_validation_exception()
    {
        // Arrange
        var userForCreation = new FakeUserForCreation()
            .RuleFor(x => x.FirstName, _ => "")
            .Generate();

        // Act & Assert
        Should.Throw<ValidationException>(() => User.Create(userForCreation));
    }

    [Fact]
    public void missing_last_name_throws_validation_exception()
    {
        // Arrange
        var userForCreation = new FakeUserForCreation()
            .RuleFor(x => x.LastName, _ => "")
            .Generate();

        // Act & Assert
        Should.Throw<ValidationException>(() => User.Create(userForCreation));
    }

    [Fact]
    public void missing_identifier_throws_validation_exception()
    {
        // Arrange
        var userForCreation = new FakeUserForCreation()
            .RuleFor(x => x.Identifier, _ => "")
            .Generate();

        // Act & Assert
        Should.Throw<ValidationException>(() => User.Create(userForCreation));
    }

    [Fact]
    public void missing_username_throws_validation_exception()
    {
        // Arrange
        var userForCreation = new FakeUserForCreation()
            .RuleFor(x => x.Username, _ => "")
            .Generate();

        // Act & Assert
        Should.Throw<ValidationException>(() => User.Create(userForCreation));
    }
}
