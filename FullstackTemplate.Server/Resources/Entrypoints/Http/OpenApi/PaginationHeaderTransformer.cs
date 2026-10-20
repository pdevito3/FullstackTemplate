namespace FullstackTemplate.Server.Resources.Entrypoints.Http.OpenApi;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

/// <summary>
/// Documents the X-Pagination response header on each operation that returns a <see cref="PagedList{T}"/>,
/// so the generated frontend contract has a type for the pagination data.
/// </summary>
public sealed class PaginationHeaderTransformer : IOpenApiOperationTransformer
{
    private const string SchemaId = nameof(PaginationMetadata);

    public async Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var returnsPagedList = context.Description.SupportedResponseTypes
            .Any(r => r.Type is { IsGenericType: true } type && type.GetGenericTypeDefinition() == typeof(PagedList<>));
        if (!returnsPagedList || operation.Responses is null || context.Document is null)
            return;

        var schema = await context.GetOrCreateSchemaAsync(typeof(PaginationMetadata), null, cancellationToken);
        context.Document.AddComponent<IOpenApiSchema>(SchemaId, schema);
        var schemaReference = new OpenApiSchemaReference(SchemaId, context.Document);

        foreach (var (statusCode, response) in operation.Responses)
        {
            if (!statusCode.StartsWith('2') || response is not OpenApiResponse openApiResponse)
                continue;

            openApiResponse.Headers ??= new Dictionary<string, IOpenApiHeader>();
            openApiResponse.Headers[PaginationMetadata.HeaderName] = new OpenApiHeader
            {
                Description = "The pagination data for the returned page, as JSON.",
                Required = true,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new() { Schema = schemaReference },
                },
            };
        }
    }
}
