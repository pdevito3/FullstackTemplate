namespace FullstackTemplate.Server.Resources.Entrypoints.Http.OpenApi;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

/// <summary>
/// Types each number only as a number. The web JSON default also reads a number from a JSON string, so the
/// generator types each number as a number or a string, with a pattern for the string. The Server still accepts
/// a number in a string, but the contract states only the canonical form.
/// </summary>
public sealed class RemoveStringFromNumbersTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (schema.Type is not { } type
            || (type & (JsonSchemaType.Integer | JsonSchemaType.Number)) == 0
            || !type.HasFlag(JsonSchemaType.String))
            return Task.CompletedTask;

        schema.Type = type & ~JsonSchemaType.String;
        schema.Pattern = null;

        return Task.CompletedTask;
    }
}
