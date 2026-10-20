namespace FullstackTemplate.Server.Resources.Entrypoints.Mcp;

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Exceptions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

/// <summary>
/// Maps a domain exception from a tool to an MCP tool error: a result with <c>isError</c> set, so the model
/// reads the message and can correct its call. The structured content has the same fields as an HTTP problem.
/// A missing required argument gives the same tool error as a missing required body field. A server fault is not
/// mapped: the SDK returns a tool error with no detail, so the message stays on the server.
/// </summary>
public static partial class McpDomainErrors
{
    // A kind with no field errors has no errors property, the same as an HTTP problem
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static McpRequestHandler<CallToolRequestParams, CallToolResult> Filter(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next) =>
        async (context, cancellationToken) =>
        {
            try
            {
                return await next(context, cancellationToken);
            }
            catch (Exception exception) when (DomainError.IsClientError(exception))
            {
                return ToToolError(DomainError.From(exception));
            }
            catch (ArgumentException exception) when (MissingArgument(exception) is { } name)
            {
                var missing = new RequestValidationException(new() { [name] = [$"The {name} field is required."] });
                return ToToolError(DomainError.From(missing));
            }
        };

    /// <summary>
    /// The code of each kind. The names match the gRPC status codes, so a caller sees one vocabulary.
    /// </summary>
    public static string CodeFor(DomainErrorKind kind) => kind switch
    {
        DomainErrorKind.Validation or DomainErrorKind.InvalidInput => "invalid_argument",
        DomainErrorKind.NotFound => "not_found",
        DomainErrorKind.Forbidden => "permission_denied",
        DomainErrorKind.Conflict => "already_exists",
        _ => "internal",
    };

    private static CallToolResult ToToolError(DomainError error)
    {
        var code = CodeFor(error.Kind);
        return new CallToolResult
        {
            IsError = true,
            Content = [new TextContentBlock { Text = $"{code}: {error.Detail ?? error.Title}" }],
            StructuredContent = JsonSerializer.SerializeToElement(
                new { code, title = error.Title, detail = error.Detail, errors = error.Errors },
                JsonOptions),
        };
    }

    // The SDK binds the arguments before the tool runs. It gives the name of the argument only in the message, for
    // example: The arguments dictionary is missing a value for the required parameter 'name'. (Parameter 'arguments')
    // The name of a tool parameter is the name in the input schema, so it is also the key of the error.
    private static string? MissingArgument(ArgumentException exception)
        => exception.ParamName == "arguments" && MissingArgumentInMessage().Match(exception.Message) is { Success: true } match
            ? match.Groups["name"].Value
            : null;

    [GeneratedRegex(@"missing a value for the required parameter '(?<name>[^']+)'", RegexOptions.CultureInvariant)]
    private static partial Regex MissingArgumentInMessage();
}
