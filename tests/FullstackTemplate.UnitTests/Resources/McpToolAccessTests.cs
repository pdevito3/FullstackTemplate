namespace FullstackTemplate.UnitTests.Resources;

using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Resources.Entrypoints;
using FullstackTemplate.Server.Resources.Entrypoints.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Shouldly;

/// <summary>
/// Each MCP tool declares what a caller needs to use it. A tool without a valid declaration stops startup.
/// </summary>
public class McpToolAccessTests
{
    [Test]
    public void each_tool_of_the_server_declares_exactly_one_valid_access_rule()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMcpServer().WithToolsFromAssembly(typeof(EntrypointRegistration).Assembly);
        using var provider = services.BuildServiceProvider();
        var tools = provider.GetRequiredService<IOptions<McpServerOptions>>().Value.ToolCollection.ShouldNotBeNull();

        // Act
        var act = () => McpToolAccess.Validate(tools);

        // Assert
        tools.ShouldNotBeEmpty();
        tools.ShouldAllBe(tool => tool.Metadata.OfType<ToolAccessAttribute>().Count() == 1);
        act.ShouldNotThrow();
    }

    [Test]
    public void tool_with_no_access_rule_stops_startup()
    {
        // Arrange
        var tool = McpServerTool.Create(() => "pong", new McpServerToolCreateOptions { Name = "ping" });

        // Act
        var act = () => McpToolAccess.Validate([tool]);

        // Assert
        act.ShouldThrow<InvalidOperationException>().Message.ShouldBe(InvalidDeclaration("ping"));
    }

    [Test]
    public void tool_that_needs_a_permission_that_does_not_exist_stops_startup()
    {
        // Arrange
        var tool = McpServerTool.Create([ToolAccess("manage_everything")] () => "pong",
            new McpServerToolCreateOptions { Name = "ping" });

        // Act
        var act = () => McpToolAccess.Validate([tool]);

        // Assert
        act.ShouldThrow<InvalidOperationException>().Message.ShouldBe(InvalidDeclaration("ping"));
    }

    [Test]
    public void tool_with_a_kind_that_is_not_defined_stops_startup()
    {
        // Arrange
        var tool = McpServerTool.Create([ToolAccess((ToolAccessKind)99)] () => "pong",
            new McpServerToolCreateOptions { Name = "ping" });

        // Act
        var act = () => McpToolAccess.Validate([tool]);

        // Assert
        act.ShouldThrow<InvalidOperationException>().Message.ShouldBe(InvalidDeclaration("ping"));
    }

    [Test]
    public void tool_that_needs_a_permission_by_its_constant_passes()
    {
        // Arrange
        var tool = McpServerTool.Create([ToolAccess(Permission.Names.ManageUsers)] () => "pong",
            new McpServerToolCreateOptions { Name = "ping" });

        // Act
        var act = () => McpToolAccess.Validate([tool]);

        // Assert
        act.ShouldNotThrow();
    }

    private static string InvalidDeclaration(string toolName)
        => $"The MCP tool {toolName} has no valid [ToolAccess] attribute. "
            + "Declare a ToolAccessKind or a Permission.Names constant.";
}
