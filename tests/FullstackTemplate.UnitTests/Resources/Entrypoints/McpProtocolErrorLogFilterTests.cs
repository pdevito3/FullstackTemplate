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
    {
        // Act & Assert
        new McpProtocolErrorLogFilter().IsEnabled(LogWith(new McpProtocolException("Unknown tool: 'x'", code)))
            .ShouldBeFalse();
    }

    [Test]
    public void a_protocol_error_of_the_server_is_logged()
    {
        // Act & Assert
        new McpProtocolErrorLogFilter().IsEnabled(LogWith(new McpProtocolException("Failed.", McpErrorCode.InternalError)))
            .ShouldBeTrue();
    }

    [Test]
    public void a_server_fault_in_a_tool_is_logged()
    {
        // Act & Assert
        new McpProtocolErrorLogFilter().IsEnabled(LogWith(new InvalidOperationException("Failed."))).ShouldBeTrue();
    }

    [Test]
    public void a_protocol_error_that_the_app_logs_is_logged()
    {
        // Act & Assert
        new McpProtocolErrorLogFilter()
            .IsEnabled(LogWith(new McpProtocolException("Bad params.", McpErrorCode.InvalidParams), "FullstackTemplate.Server.Reports"))
            .ShouldBeTrue();
    }

    private static LogEvent LogWith(Exception exception, string source = "ModelContextProtocol.Server.McpServer")
        => new(DateTimeOffset.UtcNow, LogEventLevel.Error, exception, MessageTemplate.Empty,
            [new LogEventProperty(Serilog.Core.Constants.SourceContextPropertyName, new ScalarValue(source))]);
}
