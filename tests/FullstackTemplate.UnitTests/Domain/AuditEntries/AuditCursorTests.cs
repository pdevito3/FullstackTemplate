namespace FullstackTemplate.UnitTests.Domain.AuditEntries;

using FullstackTemplate.Server.Domain.AuditEntries;
using FullstackTemplate.Server.Exceptions;
using Shouldly;

public class AuditCursorTests
{
    [Test]
    public void cursor_text_gives_back_the_key_to_the_microsecond()
    {
        // Arrange
        var cursor = new AuditCursor(new DateTimeOffset(2026, 10, 24, 14, 3, 11, TimeSpan.Zero).AddTicks(1_234_560), Guid.CreateVersion7(), 7);

        // Act
        var parsed = AuditCursor.Parse(cursor.ToString());

        // Assert
        parsed.ShouldBe(cursor);
    }

    [Test]
    [Arguments("")]
    [Arguments("abc")]
    [Arguments("1792850591123456_0192f3a8c1e47b6d9a2b3c4d5e6f7a8b")]
    [Arguments("1792850591123456_not-a-guid_3")]
    [Arguments("1792850591123456_0192f3a8c1e47b6d9a2b3c4d5e6f7a8b_-1")]
    [Arguments("x_0192f3a8c1e47b6d9a2b3c4d5e6f7a8b_3")]
    public void text_that_is_not_a_cursor_is_a_query_input_error(string text)
    {
        // Act
        Action act = () => AuditCursor.Parse(text);

        // Assert
        act.ShouldThrow<QueryInputException>().Parameter.ShouldBe("cursor");
    }
}
