namespace FullstackTemplate.UnitTests.Domain;

using FullstackTemplate.Server.Domain.Agents;
using FullstackTemplate.Server.Domain.Users;
using Shouldly;

public class SmartEnumValueObjectTests
{
    [Test]
    public void each_smart_enum_value_object_shows_as_its_value()
    {
        // Act & Assert
        UserRole.User().ToString().ShouldBe("User");
        AgentMode.Machine().ToString().ShouldBe("machine");
        Permission.ManageUsers().ToString().ShouldBe("manage_users");
    }

    [Test]
    public void a_failed_assertion_names_both_values()
    {
        // Act
        var failure = Should.Throw<ShouldAssertException>(() => UserRole.User().ShouldBe(UserRole.Admin()));

        // Assert
        failure.Message.ShouldContain("Admin");
        failure.Message.TrimEnd().ShouldEndWith("User");
        failure.Message.ShouldNotContain(typeof(UserRole).FullName!);
    }
}
