namespace FullstackTemplate.Server.Resources.Entrypoints.Http.OpenApi;

using System.Text.Json.Nodes;
using Domain;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

/// <summary>
/// Gives each property with a <see cref="FixedNamesAttribute"/> its names as an enum. A list of names gets the enum on
/// its items. The generated SPA type is then a union, so a rename on the Server breaks the SPA build. Any other schema
/// shape fails the Debug build, so that no mark is silently lost.
/// </summary>
public sealed class FixedNamesTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (context.JsonPropertyInfo?.AttributeProvider?.GetCustomAttributes(typeof(FixedNamesAttribute), false)
                is not [FixedNamesAttribute attribute])
            return Task.CompletedTask;

        if (schema.Type == JsonSchemaType.String)
            schema.Enum = EnumOf(attribute.Names);
        else if (schema.Type?.HasFlag(JsonSchemaType.Array) == true && schema.Items?.Type == JsonSchemaType.String)
            schema.Items = new OpenApiSchema { Type = JsonSchemaType.String, Enum = EnumOf(attribute.Names) };
        else
            throw new InvalidOperationException(
                $"{context.JsonPropertyInfo.DeclaringType.Name}.{context.JsonPropertyInfo.Name} has [NamesOf], but its "
                + $"schema type is {schema.Type}. Put [NamesOf] only on a string that is not nullable or on a list "
                + "of strings.");

        return Task.CompletedTask;
    }

    private static List<JsonNode> EnumOf(IEnumerable<string> names) =>
        names.Select(name => (JsonNode)JsonValue.Create(name)).ToList();
}
