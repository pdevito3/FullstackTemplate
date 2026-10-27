namespace FullstackTemplate.Server.Resources.Entrypoints.Mcp;

using ModelContextProtocol;
using Serilog.Core;
using Serilog.Events;

/// <summary>
/// Drops the logs of the MCP SDK for a protocol error that is a mistake of the caller, for example an unknown tool
/// name. The SDK logs such an error at <c>Error</c> and <c>Warning</c>, the same as a server fault. A client error is
/// not logged (see docs/backend/feature-telemetry.md), and <see cref="McpCallTelemetry"/> tags it on the span.
/// </summary>
/// <remarks>
/// The SDK logs each <see cref="McpProtocolException"/> when it sends the JSON-RPC error, after the call filters. Some
/// of these errors never get to a call filter, so a filter on the log is the only place to drop the log and keep the
/// protocol answer.
/// </remarks>
public sealed class McpProtocolErrorLogFilter : ILogEventFilter
{
    // An app that calls another MCP server and logs its -32602 has a bug of its own, so only the SDK server logs drop
    private const string SdkServerSource = "ModelContextProtocol.Server";

    public bool IsEnabled(LogEvent logEvent)
        => !(IsFromSdkServer(logEvent) && logEvent.Exception is McpProtocolException
        {
            ErrorCode: McpErrorCode.ParseError or McpErrorCode.InvalidRequest or McpErrorCode.MethodNotFound
                or McpErrorCode.InvalidParams,
        });

    private static bool IsFromSdkServer(LogEvent logEvent)
        => logEvent.Properties.TryGetValue(Constants.SourceContextPropertyName, out var source)
           && source is ScalarValue { Value: string name }
           && name.StartsWith(SdkServerSource, StringComparison.Ordinal);
}
