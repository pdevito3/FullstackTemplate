namespace FullstackTemplate.UnitTests.Databases;

using FullstackTemplate.Server.Databases;
using Shouldly;

public class LikePatternTests
{
    [Test]
    [Arguments("50%", @"%50\%%")]
    [Arguments("a_b", @"%a\_b%")]
    [Arguments(@"c\d", @"%c\\d%")]
    [Arguments(@"\%", @"%\\\%%")]
    [Arguments("plain", "%plain%")]
    public void containing_escapes_the_wildcards_and_the_escape_character(string text, string pattern)
        => LikePattern.Containing(text).ShouldBe(pattern);

    [Test]
    public void starting_with_puts_a_wildcard_only_at_the_end()
        => LikePattern.StartingWith("50%").ShouldBe(@"50\%%");

    [Test]
    public void exactly_has_no_wildcard()
        => LikePattern.Exactly("a_b").ShouldBe(@"a\_b");
}
