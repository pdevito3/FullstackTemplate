namespace FullstackTemplate.Server.Resources.Entrypoints.Http.OpenApi;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

/// <summary>
/// Documents the 401 response on each operation that requires an authenticated caller.
/// The JWT bearer challenge sends a 401 with no body, so the response has no content.
/// </summary>
/// <remarks>
/// No operation uses roles or policies, so authorization itself cannot return 403. An endpoint that throws
/// <c>ForbiddenAccessException</c> declares its 403 with <c>ProducesProblem</c>.
/// </remarks>
public sealed class UnauthorizedResponseTransformer : IOpenApiOperationTransformer
{
    private const string StatusCode = "401";

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        var requiresAuthentication = metadata.OfType<IAuthorizeData>().Any()
            && !metadata.OfType<IAllowAnonymous>().Any();
        if (!requiresAuthentication)
            return Task.CompletedTask;

        operation.Responses ??= new OpenApiResponses();
        operation.Responses.TryAdd(StatusCode, new OpenApiResponse
        {
            Description = "The request has no valid access token.",
        });

        return Task.CompletedTask;
    }
}
