namespace FullstackTemplate.UnitTests.Resources;

using FullstackTemplate.Server.Resources.Entrypoints;
using FullstackTemplate.Server.Resources.Entrypoints.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Shouldly;

/// <summary>
/// Each MCP tool tells the client if it changes data and if it touches systems outside the app. A tool that does not
/// tell stops startup, because MCP takes a hint that is not set as destructive and open to the world.
/// </summary>
public class McpToolHintsTests
{
    [Test]
    public void each_tool_of_the_server_declares_its_hints()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMcpServer().WithToolsFromAssembly(typeof(EntrypointRegistration).Assembly);
        using var provider = services.BuildServiceProvider();
        var tools = provider.GetRequiredService<IOptions<McpServerOptions>>().Value.ToolCollection.ShouldNotBeNull();

        // Act
        var act = () => McpToolHints.Validate(tools);

        // Assert
        tools.ShouldNotBeEmpty();
        act.ShouldNotThrow();
    }

    [Test]
    public void tool_with_no_hints_stops_startup_with_both_problems()
    {
        // Arrange
        var tool = McpServerTool.Create(() => "pong", new McpServerToolCreateOptions { Name = "ping" });

        // Act
        var act = () => McpToolHints.Validate([tool]);

        // Assert
        act.ShouldThrow<InvalidOperationException>().Message
            .ShouldBe(NoChangeHint("ping") + Environment.NewLine + NoOpenWorldHint("ping"));
    }

    [Test]
    public void write_tool_that_does_not_set_destructive_stops_startup()
    {
        // Arrange
        var tool = McpServerTool.Create(() => "pong",
            new McpServerToolCreateOptions { Name = "ping", ReadOnly = false, OpenWorld = false });

        // Act
        var act = () => McpToolHints.Validate([tool]);

        // Assert
        act.ShouldThrow<InvalidOperationException>().Message.ShouldBe(NoChangeHint("ping"));
    }

    [Test]
    public void tool_that_does_not_set_open_world_stops_startup()
    {
        // Arrange
        var tool = McpServerTool.Create(() => "pong",
            new McpServerToolCreateOptions { Name = "ping", ReadOnly = true });

        // Act
        var act = () => McpToolHints.Validate([tool]);

        // Assert
        act.ShouldThrow<InvalidOperationException>().Message.ShouldBe(NoOpenWorldHint("ping"));
    }

    [Test]
    [Arguments(true, null)]
    [Arguments(null, true)]
    [Arguments(null, false)]
    public void tool_that_declares_its_hints_passes(bool? readOnly, bool? destructive)
    {
        // Arrange
        var tool = McpServerTool.Create(() => "pong", new McpServerToolCreateOptions
        {
            Name = "ping", ReadOnly = readOnly, Destructive = destructive, OpenWorld = false,
        });

        // Act
        var act = () => McpToolHints.Validate([tool]);

        // Assert
        act.ShouldNotThrow();
    }

    private static string NoChangeHint(string toolName)
        => $"The MCP tool {toolName} does not declare if it changes data. "
            + "Set ReadOnly = true for a read, or set Destructive for a write.";

    private static string NoOpenWorldHint(string toolName)
        => $"The MCP tool {toolName} does not declare if it touches systems outside the app. "
            + "Set OpenWorld = false for a tool that uses only the app database.";
}
