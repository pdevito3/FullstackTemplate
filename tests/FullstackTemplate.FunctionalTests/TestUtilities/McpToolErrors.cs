namespace FullstackTemplate.FunctionalTests.TestUtilities;

using ModelContextProtocol.Protocol;
using Shouldly;

public static class McpToolErrors
{
    /// <summary>
    /// Asserts that the result is a tool error of <c>McpDomainErrors</c> with the <paramref name="code"/>, in the
    /// structured content and at the start of the text.
    /// </summary>
    public static void ShouldBeToolError(this CallToolResult result, string code)
    {
        result.IsError.ShouldBe(true);
        result.StructuredContent.ShouldNotBeNull().GetProperty("code").GetString().ShouldBe(code);
        result.Content.OfType<TextContentBlock>().ShouldHaveSingleItem().Text.ShouldStartWith($"{code}: ");
    }
}
