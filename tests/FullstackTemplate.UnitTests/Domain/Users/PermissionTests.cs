namespace FullstackTemplate.UnitTests.Domain.Users;

using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Exceptions;
using Shouldly;

public class PermissionTests
{
    [Test]
    public void each_factory_gives_its_permission_name()
    {
        // Arrange & Act & Assert
        Permission.ManageUsers().Value.ShouldBe("manage_users");
        Permission.ManageUserAccess().Value.ShouldBe("manage_user_access");
        Permission.ManageTenant().Value.ShouldBe("manage_tenant");
    }

    [Test]
    public void can_create_permission_from_string()
    {
        // Arrange & Act
        var permission = Permission.Of("manage_users");

        // Assert
        permission.Value.ShouldBe("manage_users");
    }

    [Test]
    public void permission_is_case_insensitive()
    {
        // Arrange & Act
        var permission = Permission.Of("MANAGE_USERS");

        // Assert
        permission.Value.ShouldBe("manage_users");
    }

    [Test]
    public void invalid_permission_throws_validation_exception()
    {
        // Arrange & Act & Assert
        Should.Throw<ValidationException>(() => Permission.Of("invalid_permission"));
        Should.Throw<ValidationException>(() => Permission.Of("nonexistent"));
    }

    [Test]
    public void the_removed_placeholder_permission_is_invalid()
    {
        // Arrange & Act & Assert
        Should.Throw<ValidationException>(() => Permission.Of("do_something_special"));
    }

    [Test]
    public void permission_can_be_implicitly_converted_to_string()
    {
        // Arrange
        var permission = Permission.ManageTenant();

        // Act
        string permissionString = permission;

        // Assert
        permissionString.ShouldBe("manage_tenant");
    }

    [Test]
    public void list_names_returns_all_permission_names()
    {
        // Arrange & Act
        var names = Permission.ListNames();

        // Assert
        names.ShouldBe(["manage_users", "manage_user_access", "manage_tenant", "manage_agents"], ignoreOrder: true);
    }

    [Test]
    public void get_all_returns_all_permissions()
    {
        // Arrange & Act
        var permissions = Permission.GetAll();

        // Assert
        permissions.Select(p => p.Value).ShouldBe(["manage_users", "manage_user_access", "manage_tenant", "manage_agents"], ignoreOrder: true);
    }

    [Test]
    public void permission_equality_works_correctly()
    {
        // Arrange
        var permission1 = Permission.ManageUsers();
        var permission2 = Permission.Of("manage_users");

        // Assert
        permission1.Equals(permission2).ShouldBeTrue();
        permission1.Equals(Permission.ManageTenant()).ShouldBeFalse();
    }
}
