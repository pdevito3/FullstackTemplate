namespace FullstackTemplate.Server.Resources.Extensions;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

/// <summary>
/// Keeps one media type for each request body and each response, so the generated frontend contract has one type
/// for each. The generator lists each input formatter (application/json, text/json, application/*+json) and each
/// output formatter (text/plain, application/json, text/json) for a controller. The Server still accepts the other
/// JSON media types, but the contract states only application/json for a request body. A response with a
/// <see cref="ProblemDetails"/> body is sent as application/problem+json, and each other body as application/json.
/// </summary>
public sealed class JsonContentTypeTransformer : IOpenApiOperationTransformer
{
    private const string JsonMediaType = "application/json";
    private const string ProblemJsonMediaType = "application/problem+json";

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (operation.RequestBody is OpenApiRequestBody { Content: not null } requestBody
            && requestBody.Content.TryGetValue(JsonMediaType, out var requestMediaType))
            requestBody.Content = new Dictionary<string, OpenApiMediaType> { [JsonMediaType] = requestMediaType };

        if (operation.Responses is null)
            return Task.CompletedTask;

        foreach (var (statusCode, response) in operation.Responses)
        {
            if (response is not OpenApiResponse { Content: not null } openApiResponse
                || !openApiResponse.Content.TryGetValue(JsonMediaType, out var mediaType))
                continue;

            var mediaTypeName = ReturnsProblemDetails(context, statusCode) ? ProblemJsonMediaType : JsonMediaType;
            openApiResponse.Content = new Dictionary<string, OpenApiMediaType> { [mediaTypeName] = mediaType };
        }

        return Task.CompletedTask;
    }

    private static bool ReturnsProblemDetails(OpenApiOperationTransformerContext context, string statusCode) =>
        context.Description.SupportedResponseTypes.Any(r =>
            r.StatusCode.ToString() == statusCode
            && r.Type is not null
            && typeof(ProblemDetails).IsAssignableFrom(r.Type));
}
