namespace FullstackTemplate.UnitTests.Domain.Users;

using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Exceptions;
using Shouldly;

public class UserRoleTests
{
    [Fact]
    public void can_create_admin_role()
    {
        // Arrange & Act
        var role = UserRole.Admin();

        // Assert
        role.Value.ShouldBe("Admin");
    }

    [Fact]
    public void can_create_user_role()
    {
        // Arrange & Act
        var role = UserRole.User();

        // Assert
        role.Value.ShouldBe("User");
    }

    [Fact]
    public void can_create_role_from_string()
    {
        // Arrange & Act
        var adminRole = UserRole.Of("Admin");
        var userRole = UserRole.Of("User");

        // Assert
        adminRole.Value.ShouldBe("Admin");
        userRole.Value.ShouldBe("User");
    }

    [Fact]
    public void role_is_case_insensitive()
    {
        // Arrange & Act
        var adminLower = UserRole.Of("admin");
        var adminUpper = UserRole.Of("ADMIN");
        var adminMixed = UserRole.Of("AdMiN");

        // Assert
        adminLower.Value.ShouldBe("Admin");
        adminUpper.Value.ShouldBe("Admin");
        adminMixed.Value.ShouldBe("Admin");
    }

    [Fact]
    public void invalid_role_throws_validation_exception()
    {
        // Arrange & Act & Assert
        Should.Throw<ValidationException>(() => UserRole.Of("InvalidRole"));
        Should.Throw<ValidationException>(() => UserRole.Of(""));
        Should.Throw<ValidationException>(() => UserRole.Of(null!));
    }

    [Fact]
    public void admin_role_has_default_permissions()
    {
        // Arrange
        var adminRole = UserRole.Admin();

        // Act
        var permissions = adminRole.GetDefaultPermissions();

        // Assert
        permissions.ShouldNotBeEmpty();
        permissions.ShouldContain(p => p.Value == "do_something_special");
    }

    [Fact]
    public void user_role_has_no_default_permissions()
    {
        // Arrange
        var userRole = UserRole.User();

        // Act
        var permissions = userRole.GetDefaultPermissions();

        // Assert
        permissions.ShouldBeEmpty();
    }

    [Fact]
    public void list_names_returns_all_role_names()
    {
        // Arrange & Act
        var names = UserRole.ListNames();

        // Assert
        names.ShouldContain("Admin");
        names.ShouldContain("User");
        names.Count.ShouldBe(2);
    }
}
