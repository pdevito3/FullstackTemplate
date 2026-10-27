namespace FullstackTemplate.Server.Resources.Entrypoints.Http.OpenApi;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

/// <summary>
/// Marks each property that is not nullable as required. The generator marks only C# <c>required</c>
/// members as required, so without this transformer each DTO property is optional in the frontend contract.
/// A nullable property that refers to another component schema is still a bare reference here, because the generator
/// adds its null option after the schema transformers run. So the transformer also reads the nullable annotation of
/// the C# property.
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

        var annotatedNullable = context.JsonTypeInfo.Properties
            .Where(p => p.IsGetNullable)
            .Select(p => p.Name)
            .ToHashSet();
        schema.Required ??= new HashSet<string>();
        foreach (var (name, property) in schema.Properties)
        {
            if (!IsNullable(property) && !annotatedNullable.Contains(name))
                schema.Required.Add(name);
        }

        return Task.CompletedTask;
    }

    private static bool IsNullable(IOpenApiSchema property) =>
        property.Type?.HasFlag(JsonSchemaType.Null) == true
        || property.OneOf?.Any(s => s.Type?.HasFlag(JsonSchemaType.Null) == true) == true
        || property.AnyOf?.Any(s => s.Type?.HasFlag(JsonSchemaType.Null) == true) == true;
}
