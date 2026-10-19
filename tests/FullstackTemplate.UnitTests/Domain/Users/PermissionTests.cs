namespace FullstackTemplate.UnitTests.Domain.Users;

using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Exceptions;
using Shouldly;

public class PermissionTests
{
    [Fact]
    public void can_create_do_something_special_permission()
    {
        // Arrange & Act
        var permission = Permission.DoSomethingSpecial();

        // Assert
        permission.Value.ShouldBe("do_something_special");
    }

    [Fact]
    public void can_create_permission_from_string()
    {
        // Arrange & Act
        var permission = Permission.Of("do_something_special");

        // Assert
        permission.Value.ShouldBe("do_something_special");
    }

    [Fact]
    public void permission_is_case_insensitive()
    {
        // Arrange & Act
        var permission = Permission.Of("DO_SOMETHING_SPECIAL");

        // Assert
        permission.Value.ShouldBe("do_something_special");
    }

    [Fact]
    public void invalid_permission_throws_validation_exception()
    {
        // Arrange & Act & Assert
        Should.Throw<ValidationException>(() => Permission.Of("invalid_permission"));
        Should.Throw<ValidationException>(() => Permission.Of("nonexistent"));
    }

    [Fact]
    public void permission_can_be_implicitly_converted_to_string()
    {
        // Arrange
        var permission = Permission.DoSomethingSpecial();

        // Act
        string permissionString = permission;

        // Assert
        permissionString.ShouldBe("do_something_special");
    }

    [Fact]
    public void list_names_returns_all_permission_names()
    {
        // Arrange & Act
        var names = Permission.ListNames();

        // Assert
        names.ShouldContain("do_something_special");
    }

    [Fact]
    public void get_all_returns_all_permissions()
    {
        // Arrange & Act
        var permissions = Permission.GetAll();

        // Assert
        permissions.ShouldNotBeEmpty();
        permissions.ShouldContain(p => p.Value == "do_something_special");
    }

    [Fact]
    public void permission_equality_works_correctly()
    {
        // Arrange
        var permission1 = Permission.DoSomethingSpecial();
        var permission2 = Permission.DoSomethingSpecial();

        // Assert
        permission1.Equals(permission2).ShouldBeTrue();
    }
}
