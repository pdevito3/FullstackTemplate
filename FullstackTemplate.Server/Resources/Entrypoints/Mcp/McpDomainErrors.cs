namespace FullstackTemplate.Server.Resources.Entrypoints.Mcp;

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Exceptions;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

/// <summary>
/// Maps a domain exception from a tool to an MCP tool error: a result with <c>isError</c> set, so the model
/// reads the message and can correct its call. The structured content has the same fields as an HTTP problem.
/// A missing required argument gives the same tool error as a missing required body field. An argument of the wrong
/// JSON type gives the message of a query value that does not bind. A server fault is not mapped: the SDK returns a tool
/// error with no detail, so the message stays on the server.
/// </summary>
public static partial class McpDomainErrors
{
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
            catch (JsonException) when (WrongTypeArguments(context) is { Count: > 0 } errors)
            {
                return ToToolError(DomainError.From(new RequestValidationException(errors)));
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
        DomainErrorKind.StaleVersion or DomainErrorKind.ConcurrentChange => "aborted",
        DomainErrorKind.Unexpected => "internal",
    };

    private static CallToolResult ToToolError(DomainError error)
    {
        var code = CodeFor(error.Kind);
        return new CallToolResult
        {
            IsError = true,
            Content = [new TextContentBlock { Text = $"{code}: {error.Detail ?? error.Title}" }],
            StructuredContent = JsonSerializer.SerializeToElement(
                new McpToolError(code, error.Title, error.Detail, error.Errors), EntrypointJsonContext.Default.McpToolError),
        };
    }

    // The SDK binds the arguments before the tool runs. It gives the name of the argument only in the message, for
    // example: The arguments dictionary is missing a value for the required parameter 'name'. (Parameter 'arguments')
    // The name of a tool parameter is the name in the input schema, so it is also the key of the error.
    private static string? MissingArgument(ArgumentException exception)
        => exception.ParamName == "arguments" && MissingArgumentInMessage().Match(exception.Message) is { Success: true } match
            ? match.Groups["name"].Value
            : null;

    // The SDK converts each argument before the tool runs, and its JsonException does not name the argument.
    // The SDK fills the parameters that are not in the input schema, such as the cancellation token, and ignores an
    // argument with their name.
    private static Dictionary<string, string[]> WrongTypeArguments(RequestContext<CallToolRequestParams> context)
    {
        if (context.MatchedPrimitive is not McpServerTool tool
            || tool.Metadata.OfType<MethodInfo>().FirstOrDefault() is not { } method
            || !tool.ProtocolTool.InputSchema.TryGetProperty("properties", out var properties)
            || context.Params?.Arguments is not { } arguments)
            return [];

        var types = method.GetParameters()
            .Where(p => p.Name is not null && properties.TryGetProperty(p.Name, out _))
            .ToDictionary(p => p.Name!, p => p.ParameterType);
        return arguments
            .Where(argument => types.TryGetValue(argument.Key, out var type) && !Converts(argument.Value, type))
            .ToDictionary(
                argument => argument.Key,
                argument => new[] { $"The value '{Shown(argument.Value)}' is not valid for {argument.Key}." });
    }

    private static bool Converts(JsonElement value, Type type)
    {
        try
        {
            JsonSerializer.Deserialize(value, type, McpJsonUtilities.DefaultOptions);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private const int MaxShownLength = 100;

    private static string Shown(JsonElement value)
    {
        var shown = value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();
        return shown.Length > MaxShownLength ? $"{Cut(shown, MaxShownLength)}..." : shown;
    }

    private static string Cut(string text, int length)
        => char.IsHighSurrogate(text[length - 1]) ? text[..(length - 1)] : text[..length];

    [GeneratedRegex(@"missing a value for the required parameter '(?<name>[^']+)'", RegexOptions.CultureInvariant)]
    private static partial Regex MissingArgumentInMessage();
}

// A kind with no field errors has no errors property, the same as an HTTP problem
internal sealed record McpToolError(
    string Code,
    string Title,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Detail,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Dictionary<string, string[]>? Errors);
