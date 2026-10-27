namespace FullstackTemplate.UnitTests.Resources.Entrypoints;

using FullstackTemplate.Server.Resources.Entrypoints.Mcp;
using ModelContextProtocol;
using Serilog.Events;
using Shouldly;

public class McpProtocolErrorLogFilterTests
{
    [Test]
    [Arguments(McpErrorCode.ParseError)]
    [Arguments(McpErrorCode.InvalidRequest)]
    [Arguments(McpErrorCode.MethodNotFound)]
    [Arguments(McpErrorCode.InvalidParams)]
    public void a_protocol_error_of_the_caller_is_not_logged(McpErrorCode code)
        => new McpProtocolErrorLogFilter().IsEnabled(LogWith(new McpProtocolException("Unknown tool: 'x'", code)))
            .ShouldBeFalse();

    [Test]
    public void a_protocol_error_of_the_server_is_logged()
        => new McpProtocolErrorLogFilter().IsEnabled(LogWith(new McpProtocolException("Failed.", McpErrorCode.InternalError)))
            .ShouldBeTrue();

    [Test]
    public void a_server_fault_in_a_tool_is_logged()
        => new McpProtocolErrorLogFilter().IsEnabled(LogWith(new InvalidOperationException("Failed."))).ShouldBeTrue();

    private static LogEvent LogWith(Exception exception)
        => new(DateTimeOffset.UtcNow, LogEventLevel.Error, exception, MessageTemplate.Empty, []);
}
