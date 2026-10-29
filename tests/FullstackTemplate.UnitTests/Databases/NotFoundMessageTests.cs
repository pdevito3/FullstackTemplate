namespace FullstackTemplate.UnitTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Exceptions;
using Shouldly;

public class NotFoundMessageTests
{
    [Test]
    public void a_missing_entity_is_named_in_words()
    {
        // Arrange
        WorkOrder? missing = null;

        // Act
        var find = () => missing.MustBeFoundOrThrow();

        // Assert
        find.ShouldThrow<NotFoundException>().Message.ShouldBe("Work order was not found.");
    }

    private sealed class WorkOrder() : BaseEntity("wo");
}
