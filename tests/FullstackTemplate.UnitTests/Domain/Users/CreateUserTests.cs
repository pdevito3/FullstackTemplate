namespace FullstackTemplate.UnitTests.Domain.Users;

using FullstackTemplate.Server.Domain.Tenants;
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

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void cannot_create_user_with_blank_email(string email)
    {
        // Arrange
        var userForCreation = new FakeUserForCreation()
            .RuleFor(x => x.Email, _ => email)
            .Generate();

        // Act
        var act = () => User.Create(userForCreation);

        // Assert
        act.ShouldThrow<ValidationException>().Message.ShouldBe("Please provide a valid email.");
    }

    public static TheoryData<string?, string> InvalidTenantIds => new()
    {
        { null, "Please provide a tenant." },
        { $"{User.IdPrefix}_0199f0c1a2b37c4d8e9f0a1b2c3d4e5f", "Please provide a valid tenant ID." },
        { $"{Tenant.IdPrefix}_{new string('a', 47)}", "Please provide a valid tenant ID." },
        { $"{Tenant.IdPrefix}_0199F0C1A2B37C4D8E9F0A1B2C3D4E5F", "Please provide a valid tenant ID." },
    };

    [Theory]
    [MemberData(nameof(InvalidTenantIds))]
    public void cannot_create_user_with_invalid_tenant_id(string? tenantId, string message)
    {
        // Arrange
        var userForCreation = new FakeUserForCreation()
            .RuleFor(x => x.TenantId, _ => tenantId!)
            .Generate();

        // Act
        var act = () => User.Create(userForCreation);

        // Assert
        act.ShouldThrow<ValidationException>().Message.ShouldBe(message);
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
