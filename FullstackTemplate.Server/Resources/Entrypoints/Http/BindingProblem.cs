namespace FullstackTemplate.Server.Resources.Entrypoints.Http;

using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Gives the errors of a 400 when a minimal API cannot bind a request value: a query value that is not a number,
/// a missing route value, or a body that is not valid JSON. <c>RouteHandlerOptions.ThrowOnBadRequest</c> makes the
/// endpoint throw a <see cref="BadHttpRequestException"/>, and <see cref="DomainProblemDetails"/> uses these errors.
/// The result has the same shape as the 400 of MVC model binding. The framework stops at the first value that does not
/// bind, so the result names only that value.
/// </summary>
public static partial class BindingProblem
{
    public const string Title = "One or more validation errors occurred.";

    /// <summary>The message for a missing or null body. It is the message of MVC, and its key is the empty string.</summary>
    public const string MissingBodyMessage = "A non-empty request body is required.";

    /// <summary>The messages for each request value that did not bind, keyed by the name of the value.</summary>
    public static Dictionary<string, string[]> ErrorsFor(BadHttpRequestException exception)
    {
        // The framework message names the C# parameter and suggests a service, so the caller gets the fixed message
        if (IsMissingBody(exception))
            return new() { [string.Empty] = [MissingBodyMessage] };

        if (MissingMembersOf(exception) is { Length: > 0 } missing)
            return missing.ToDictionary(name => name, name => new[] { MissingMemberMessage(name) });

        if (WrongTypeFieldOf(exception) is { } field)
            return new() { [field] = [WrongTypeMessage(field, WrongTypeKindOf(exception))] };

        if (NotJsonFieldOf(exception) is { } notJsonField)
            return new() { [notJsonField] = [NotJsonMessage(notJsonField)] };

        // Another JSON error, such as one from a converter, is not a field error, so the key is the JSON path, as in MVC
        if (exception.InnerException is JsonException jsonException)
            return new() { [jsonException.Path ?? "$"] = [jsonException.Message] };

        // The framework gives the parameter only in the message, for example: Failed to bind parameter "int? pageNumber" from "abc".
        // The name of a handler parameter is the name in the query string, so it is also the key of the error.
        var match = ParameterInMessage().Match(exception.Message);
        return new() { [match.Success ? match.Groups["name"].Value : string.Empty] = [MessageFor(exception)] };
    }

    /// <summary>The message for the detail of the problem.</summary>
    public static string DetailFor(BadHttpRequestException exception)
    {
        if (IsMissingBody(exception))
            return MissingBodyMessage;
        if (MissingMembersOf(exception) is { Length: > 0 } missing)
            return string.Join(" ", missing.Select(MissingMemberMessage));
        if (WrongTypeFieldOf(exception) is { } field)
            return WrongTypeMessage(field, WrongTypeKindOf(exception));
        if (NotJsonFieldOf(exception) is { } notJsonField)
            return NotJsonMessage(notJsonField);
        return exception.InnerException is JsonException jsonException ? jsonException.Message : MessageFor(exception);
    }

    // The framework message gives the C# type, for example "int? pageNumber". The caller gets the name and the value only.
    private static string MessageFor(BadHttpRequestException exception)
        => ValueInMessage().Match(exception.Message) is { Success: true } match
            ? $"The value '{match.Groups["value"].Value}' is not valid for {match.Groups["name"].Value}."
            : exception.Message;

    // A body without a required member gives "JSON deserialization for type 'TenantForUpdateDto' was missing
    // required properties including: 'version'." The message names the C# type, so the caller gets the JSON names only.
    private static string[] MissingMembersOf(BadHttpRequestException exception)
        => exception.InnerException is JsonException jsonException
           && MissingMembersInMessage().Match(jsonException.Message) is { Success: true } match
            ? match.Groups["name"].Captures.Select(c => c.Value).ToArray()
            : [];

    private static string MissingMemberMessage(string name) => $"The {name} field is required.";

    // A value of the wrong type, for example a text, a null, or a value out of range for a number, gives "The JSON value
    // could not be converted to System.UInt32. Path: $.version | LineNumber: 0 | BytePositionInLine: 18." The message
    // names the C# type, so the caller gets the field and the kind of value that the field takes.
    private static string? WrongTypeFieldOf(BadHttpRequestException exception)
        => exception.InnerException is JsonException jsonException && WrongTypeInMessage().IsMatch(jsonException.Message)
            ? KeyOf(jsonException)
            : null;

    private static string WrongTypeMessage(string key, string? kind)
        => key.Length == 0 ? "The request body is not valid."
            : kind is null ? $"The {FieldNameOf(key)} field is not valid."
            : $"The {FieldNameOf(key)} field is not a valid {kind}.";

    private static string? WrongTypeKindOf(BadHttpRequestException exception)
        => exception.InnerException is JsonException jsonException
           && WrongTypeInMessage().Match(jsonException.Message) is { Success: true } match
            ? ValueKinds.GetValueOrDefault(match.Groups["type"].Value)
            : null;

    private static readonly Dictionary<string, string> ValueKinds = new()
    {
        ["System.Byte"] = "whole number",
        ["System.SByte"] = "whole number",
        ["System.Int16"] = "whole number",
        ["System.UInt16"] = "whole number",
        ["System.Int32"] = "whole number",
        ["System.UInt32"] = "whole number",
        ["System.Int64"] = "whole number",
        ["System.UInt64"] = "whole number",
        ["System.Single"] = "number",
        ["System.Double"] = "number",
        ["System.Decimal"] = "number",
        ["System.DateTime"] = "date and time",
        ["System.DateTimeOffset"] = "date and time",
        ["System.DateOnly"] = "date",
    };

    // A value that is not JSON, for example 1.2.3 for a number, gives "'.' is an invalid end of a number. Expected 'E'
    // or 'e'. Path: $.version | LineNumber: 0 | BytePositionInLine: 32." The error of the JSON reader is the inner
    // exception. The message shows the position in the body, so the caller gets the field only.
    private static string? NotJsonFieldOf(BadHttpRequestException exception)
        => exception.InnerException is JsonException { InnerException: JsonException } jsonException
            ? KeyOf(jsonException)
            : null;

    private static string NotJsonMessage(string key)
        => key.Length == 0 ? "The request body is not valid JSON." : $"The {FieldNameOf(key)} field is not valid JSON.";

    // The path "$.lines[1].hours" gives the key "lines[1].hours", and the path "$" of the body gives the empty key
    private static string? KeyOf(JsonException exception)
        => exception.Path is ['$', .. var rest] ? rest is ['.', .. var field] ? field : rest : null;

    // The message names only the field, as a request validation error does, so the key "lines[1].hours" gives "hours"
    private static string FieldNameOf(string key)
        => FieldNameInKey().Match(key) is { Success: true } match ? match.Groups["name"].Value : key;

    // An empty body and a JSON null give "Implicit body inferred for parameter "dto" but no body was provided."
    // An explicit [FromBody] gives "Required parameter "TenantForCreationDto dto" was not provided from body."
    private static bool IsMissingBody(BadHttpRequestException exception)
        => exception.InnerException is null && MissingBodyInMessage().IsMatch(exception.Message);

    [GeneratedRegex(@"parameter ""(?:[^""]* )?(?<name>[^"" ]+)""", RegexOptions.CultureInvariant)]
    private static partial Regex ParameterInMessage();

    [GeneratedRegex(@"^Failed to bind parameter ""(?:[^""]* )?(?<name>[^"" ]+)"" from ""(?<value>.*)""\.$", RegexOptions.CultureInvariant)]
    private static partial Regex ValueInMessage();

    [GeneratedRegex(@"was missing required properties including: (?:'(?<name>[^']+)'[^']*)+$", RegexOptions.CultureInvariant)]
    private static partial Regex MissingMembersInMessage();

    // The type is a whole name such as System.Int32 or System.Nullable`1[System.Int32]. An array such as
    // System.Int32[] has no type group, so a list field does not get the kind of its items.
    [GeneratedRegex(@"^The JSON value could not be converted to (?:(?:System\.Nullable`1\[(?<type>\w+(?:\.\w+)*)\]|(?<type>\w+(?:\.\w+)*))\.(?: |$))?", RegexOptions.CultureInvariant)]
    private static partial Regex WrongTypeInMessage();

    [GeneratedRegex(@"(?<name>[^.\[\]]+)(?:\[\d+\])*$", RegexOptions.CultureInvariant)]
    private static partial Regex FieldNameInKey();

    [GeneratedRegex(@"but no body was provided|was not provided from body", RegexOptions.CultureInvariant)]
    private static partial Regex MissingBodyInMessage();
}
