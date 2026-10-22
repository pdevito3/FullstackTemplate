namespace FullstackTemplate.UnitTests.Resources.Entrypoints;

using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.Server.Resources.Entrypoints.Mcp;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using NSubstitute;
using Shouldly;

/// <summary>
/// No tool updates a record yet, so these tests drive the filter with a tool that throws.
/// </summary>
public class McpDomainErrorsTests
{
    private const string StaleMessage = "This record changed after you loaded it. Reload it, then make your change again.";

    [Test]
    public async Task a_stale_version_is_an_aborted_tool_error_with_the_message_under_the_version()
    {
        // Act
        var result = await CallToolThatThrows(new StaleVersionException());

        // Assert
        result.IsError.ShouldBe(true);
        result.Content.OfType<TextContentBlock>().ShouldHaveSingleItem().Text.ShouldBe($"aborted: {StaleMessage}");
        var content = result.StructuredContent.ShouldNotBeNull();
        content.GetProperty("code").GetString().ShouldBe("aborted");
        content.GetProperty("title").GetString().ShouldBe("The record changed after you loaded it.");
        content.GetProperty("errors").GetProperty("version")[0].GetString().ShouldBe(StaleMessage);
    }

    [Test]
    public async Task a_conflict_is_an_already_exists_tool_error()
    {
        // Act
        var result = await CallToolThatThrows(new ConflictException("name", "A tenant with this name already exists."));

        // Assert
        result.IsError.ShouldBe(true);
        result.Content.OfType<TextContentBlock>().ShouldHaveSingleItem().Text
            .ShouldBe("already_exists: A tenant with this name already exists.");
        var content = result.StructuredContent.ShouldNotBeNull();
        content.GetProperty("code").GetString().ShouldBe("already_exists");
        content.GetProperty("title").GetString().ShouldBe("The request conflicts with a record that exists.");
    }

    private static async Task<CallToolResult> CallToolThatThrows(Exception exception)
    {
        var handler = McpDomainErrors.Filter((_, _) => throw exception);
        var request = new RequestContext<CallToolRequestParams>(
            Substitute.For<McpServer>(),
            new JsonRpcRequest { Method = RequestMethods.ToolsCall },
            new CallToolRequestParams { Name = "update_tenant" });
        return await handler(request, CancellationToken.None);
    }
}
