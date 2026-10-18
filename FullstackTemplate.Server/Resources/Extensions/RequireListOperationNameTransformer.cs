namespace FullstackTemplate.Server.Resources.Extensions;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

/// <summary>
/// Fails document generation when the operationId and the X-Pagination header disagree. The frontend
/// contract makes each client whose operationId ends in <c>List</c> return the full response, so that
/// the caller can read the header. Thus an operation that sends the header must have a name that ends
/// in <c>List</c>, and an operation with such a name must send the header.
/// </summary>
public sealed class RequireListOperationNameTransformer : IOpenApiDocumentTransformer
{
    private const string ListSuffix = "List";

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var operations = document.Paths
            .SelectMany(path => (path.Value.Operations ?? [])
                .Select(operation => (
                    Endpoint: $"{operation.Key.Method.ToUpperInvariant()} {path.Key}",
                    OperationId: operation.Value.OperationId ?? "",
                    HasPaginationHeader: HasPaginationHeader(operation.Value))))
            .ToList();

        var errors = new List<string>();

        var unlistedPaged = operations
            .Where(o => o.HasPaginationHeader && !o.OperationId.EndsWith(ListSuffix, StringComparison.Ordinal))
            .Select(o => $"{o.Endpoint} ({o.OperationId})")
            .ToList();
        if (unlistedPaged.Count > 0)
            errors.Add(
                $"These endpoints return a PagedList and send the {PaginationMetadata.HeaderName} header, but their name does not end in '{ListSuffix}': "
                + string.Join(", ", unlistedPaged)
                + $". The frontend client of such an endpoint returns only the body, so the caller cannot read the header. Give the endpoint a name that ends in '{ListSuffix}', for example 'GetTenantList'.");

        var listsWithoutPaging = operations
            .Where(o => !o.HasPaginationHeader && o.OperationId.EndsWith(ListSuffix, StringComparison.Ordinal))
            .Select(o => $"{o.Endpoint} ({o.OperationId})")
            .ToList();
        if (listsWithoutPaging.Count > 0)
            errors.Add(
                $"These endpoints have a name that ends in '{ListSuffix}', but they do not send the {PaginationMetadata.HeaderName} header: "
                + string.Join(", ", listsWithoutPaging)
                + $". The frontend client of such an endpoint returns the full response instead of the body. Return a PagedList<T> and call Response.AddPaginationHeader, or give the endpoint a name that does not end in '{ListSuffix}'.");

        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(" ", errors));

        return Task.CompletedTask;
    }

    private static bool HasPaginationHeader(OpenApiOperation operation) =>
        (operation.Responses ?? [])
            .Where(response => response.Key.StartsWith('2'))
            .Any(response => response.Value.Headers?.ContainsKey(PaginationMetadata.HeaderName) == true);
}
