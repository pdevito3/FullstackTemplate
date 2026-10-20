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

        // A JSON body error names the JSON path, for example "$.name", the same as MVC
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
        return exception.InnerException is JsonException jsonException ? jsonException.Message : MessageFor(exception);
    }

    // The framework message gives the C# type, for example "int? pageNumber". The caller gets the name and the value only.
    private static string MessageFor(BadHttpRequestException exception)
        => ValueInMessage().Match(exception.Message) is { Success: true } match
            ? $"The value '{match.Groups["value"].Value}' is not valid for {match.Groups["name"].Value}."
            : exception.Message;

    // An empty body and a JSON null give "Implicit body inferred for parameter "dto" but no body was provided."
    // An explicit [FromBody] gives "Required parameter "TenantForCreationDto dto" was not provided from body."
    private static bool IsMissingBody(BadHttpRequestException exception)
        => exception.InnerException is null && MissingBodyInMessage().IsMatch(exception.Message);

    [GeneratedRegex(@"parameter ""(?:[^""]* )?(?<name>[^"" ]+)""", RegexOptions.CultureInvariant)]
    private static partial Regex ParameterInMessage();

    [GeneratedRegex(@"^Failed to bind parameter ""(?:[^""]* )?(?<name>[^"" ]+)"" from ""(?<value>.*)""\.$", RegexOptions.CultureInvariant)]
    private static partial Regex ValueInMessage();

    [GeneratedRegex(@"but no body was provided|was not provided from body", RegexOptions.CultureInvariant)]
    private static partial Regex MissingBodyInMessage();
}
