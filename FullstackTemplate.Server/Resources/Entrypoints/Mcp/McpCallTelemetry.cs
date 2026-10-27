namespace FullstackTemplate.Server.Resources.Entrypoints.Mcp;

using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Services;

/// <summary>
/// Puts the tool name, the caller, and the error of a tool call on the span of its HTTP request. A call that fails
/// before its feature runs has no feature span: a call that <see cref="McpToolAccess"/> denies, an argument of the wrong
/// type, or an unknown tool. For such a call, this span is the only one that names the tool and the caller.
/// </summary>
/// <remarks>
/// The transport is stateless, so each HTTP request has exactly one tool call. The error is the <c>code</c> of a tool
/// error, for example <c>permission_denied</c>, or the type of the exception that the SDK answers.
/// </remarks>
public static class McpCallTelemetry
{
    // The longest tool name that the MCP spec allows. An unknown name comes from the caller, so it can be of any length.
    private const int MaxToolNameLength = 128;

    /// <summary>Add it before each other call filter, so that it sees the tool error of each one.</summary>
    public static McpRequestHandler<CallToolRequestParams, CallToolResult> Filter(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next) =>
        async (context, cancellationToken) =>
        {
            var services = context.Services
                ?? throw new InvalidOperationException("The MCP request has no services, so it cannot load the caller.");
            var span = services.GetRequiredService<IHttpContextAccessor>().HttpContext?.Features
                .Get<IHttpActivityFeature>()?.Activity;

            span?.SetTag("gen_ai.tool.name", ToolNameOf(context.Params?.Name));

            try
            {
                var caller = await services.GetRequiredService<ICallerIdentityResolver>().GetAsync(cancellationToken);
                foreach (var (tag, _, value) in CallerTelemetry.Properties(caller))
                    span?.SetTag(tag, value);

                var result = await next(context, cancellationToken);
                if (result.IsError is true)
                    span?.SetTag("error.type", CodeOf(result) ?? "tool_error");
                return result;
            }
            catch (Exception exception)
            {
                span?.SetTag("error.type", exception.GetType().FullName);
                throw;
            }
        };

    private static string? ToolNameOf(string? name)
    {
        var clean = LogValue.Clean(name);
        return clean?.Length > MaxToolNameLength ? clean[..MaxToolNameLength] : clean;
    }

    private static string? CodeOf(CallToolResult result)
        => result.StructuredContent is { ValueKind: JsonValueKind.Object } content
           && content.TryGetProperty("code", out var code)
           && code.ValueKind == JsonValueKind.String
            ? code.GetString()
            : null;
}
