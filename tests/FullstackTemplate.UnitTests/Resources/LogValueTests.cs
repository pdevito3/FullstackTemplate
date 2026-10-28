namespace FullstackTemplate.UnitTests.Resources;

using FullstackTemplate.Server.Resources;
using Shouldly;

public class LogValueTests
{
    [Test]
    [Arguments("/a\r\nforged", "/aforged")]
    [Arguments("/a\nb", "/ab")]
    [Arguments("/a\rb", "/ab")]
    [Arguments("\t/a\u0000b\u001b[31m", "/ab[31m")]
    [Arguments("/a\u007fb", "/ab")]
    [Arguments("/a\u0085b\u009fc", "/abc")]
    [Arguments("/a\u2028b\u2029c", "/abc")]
    [Arguments("\r\n", "")]
    public void control_characters_are_removed(string value, string expected)
    {
        LogValue.Clean(value).ShouldBe(expected);
    }

    [Test]
    [Arguments("/api/v1/items?city=x")]
    [Arguments("/caf\u00e9 \u4e2d")]
    public void a_clean_value_is_the_same_string(string value)
    {
        LogValue.Clean(value).ShouldBeSameAs(value);
    }

    [Test]
    public void null_and_empty_stay_as_they_are()
    {
        LogValue.Clean(null).ShouldBeNull();
        LogValue.Clean("").ShouldBe("");
    }
}
