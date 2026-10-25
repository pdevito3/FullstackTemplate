namespace FullstackTemplate.UnitTests.Exceptions;

using FullstackTemplate.Server.Exceptions;
using Shouldly;

public class CharacterPostgresCannotStoreTests
{
    [Test]
    [Arguments("")]
    [Arguments("a\u0001b")]
    [Arguments("a\tb")]
    [Arguments("a\nb")]
    [Arguments("a\u007Fb")]
    [Arguments("a\u0085b")]
    [Arguments("a￿b")]
    [Arguments("a😀b")]
    [Arguments("a😀")]
    public void a_value_that_postgres_can_store_passes(string value)
    {
        // Act
        var check = () => ValidationException.ThrowWhenHasCharacterPostgresCannotStore(value, "name", "Not allowed.");

        // Assert
        check.ShouldNotThrow();
    }

    [Test]
    [Arguments("\0")]
    [Arguments("a\0")]
    [Arguments("a\uD800")]
    [Arguments("\uDC00a")]
    [Arguments("a\uDE00\uD83Db")]
    [Arguments("a\uD83D😀")]
    public void a_value_with_a_nul_or_a_lone_surrogate_fails(string value)
    {
        // Act
        var check = () => ValidationException.ThrowWhenHasCharacterPostgresCannotStore(value, "name", "Not allowed.");

        // Assert
        check.ShouldThrow<ValidationException>().Property.ShouldBe("name");
    }
}
