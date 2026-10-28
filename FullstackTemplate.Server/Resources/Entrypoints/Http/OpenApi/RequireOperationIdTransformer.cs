namespace FullstackTemplate.Server.Resources.Entrypoints.Http.OpenApi;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

/// <summary>
/// Fails document generation when an operation has no operationId. The frontend contract names each
/// generated file from the operationId, so a fallback name from the route causes file churn.
/// Give each endpoint a name: <c>[HttpGet(Name = "GetTenant")]</c> or <c>.WithName("GetTenant")</c>.
/// </summary>
public sealed class RequireOperationIdTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var unnamed = document.Paths
            .SelectMany(path => (path.Value.Operations ?? [])
                .Where(operation => string.IsNullOrWhiteSpace(operation.Value.OperationId))
                .Select(operation => $"{operation.Key.Method.ToUpperInvariant()} {path.Key}"))
            .ToList();

        if (unnamed.Count > 0)
            throw new InvalidOperationException(
                "These endpoints have no name, so the OpenAPI document has no operationId for them: "
                + string.Join(", ", unnamed)
                + ". Give each endpoint a route name or call .WithName().");

        return Task.CompletedTask;
    }
}
