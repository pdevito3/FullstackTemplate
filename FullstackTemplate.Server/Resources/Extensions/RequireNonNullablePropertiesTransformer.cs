namespace FullstackTemplate.Server.Resources.Extensions;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

/// <summary>
/// Marks each property that is not nullable as required. The generator marks only C# <c>required</c>
/// members as required, so without this transformer each DTO property is optional in the frontend contract.
/// </summary>
public sealed class RequireNonNullablePropertiesTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (schema.Properties is not { Count: > 0 })
            return Task.CompletedTask;

        schema.Required ??= new HashSet<string>();
        foreach (var (name, property) in schema.Properties)
        {
            if (!IsNullable(property))
                schema.Required.Add(name);
        }

        return Task.CompletedTask;
    }

    private static bool IsNullable(IOpenApiSchema property) =>
        property.Type?.HasFlag(JsonSchemaType.Null) == true
        || property.OneOf?.Any(s => s.Type?.HasFlag(JsonSchemaType.Null) == true) == true
        || property.AnyOf?.Any(s => s.Type?.HasFlag(JsonSchemaType.Null) == true) == true;
}
