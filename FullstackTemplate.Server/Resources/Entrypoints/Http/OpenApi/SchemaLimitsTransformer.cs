namespace FullstackTemplate.Server.Resources.Entrypoints.Http.OpenApi;

using System.Globalization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

/// <summary>
/// Gives each property with a <see cref="SchemaMaxLengthAttribute"/> its <c>maxLength</c>, each property with a
/// <see cref="SchemaMinimumAttribute"/> its <c>minimum</c>, and each property with a
/// <see cref="SchemaMaximumAttribute"/> its <c>maximum</c>. The generated Zod schema of the SPA then has the limit, so a
/// form reads it from the contract and not from a copy that can go stale. A max length on a schema that is not a string,
/// or a minimum or a maximum on a schema that is not a number, fails the Debug build, so that no mark is silently lost.
/// </summary>
public sealed class SchemaLimitsTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (context.JsonPropertyInfo is not { AttributeProvider: { } attributes } property)
            return Task.CompletedTask;

        var type = schema.Type & ~JsonSchemaType.Null;

        if (attributes.GetCustomAttributes(typeof(SchemaMaxLengthAttribute), false)
            is [SchemaMaxLengthAttribute maxLength])
        {
            if (type != JsonSchemaType.String)
                throw WrongSchemaType(property, "SchemaMaxLength", schema, "a string");
            schema.MaxLength = maxLength.MaxLength;
        }

        if (attributes.GetCustomAttributes(typeof(SchemaMinimumAttribute), false)
            is [SchemaMinimumAttribute minimum])
        {
            if (type is not (JsonSchemaType.Integer or JsonSchemaType.Number))
                throw WrongSchemaType(property, "SchemaMinimum", schema, "a number");
            schema.Minimum = minimum.Minimum.ToString(CultureInfo.InvariantCulture);
        }

        if (attributes.GetCustomAttributes(typeof(SchemaMaximumAttribute), false)
            is [SchemaMaximumAttribute maximum])
        {
            if (type is not (JsonSchemaType.Integer or JsonSchemaType.Number))
                throw WrongSchemaType(property, "SchemaMaximum", schema, "a number");
            schema.Maximum = maximum.Maximum.ToString(CultureInfo.InvariantCulture);
        }

        if (schema is { Minimum: { } low, Maximum: { } high }
            && decimal.Parse(low, CultureInfo.InvariantCulture) > decimal.Parse(high, CultureInfo.InvariantCulture))
            throw NoValuePasses(property, low, high);

        return Task.CompletedTask;
    }

    private static InvalidOperationException WrongSchemaType(
        JsonPropertyInfo property, string attribute, OpenApiSchema schema, string allowed) =>
        new($"{property.DeclaringType.Name}.{property.Name} has [{attribute}], but its schema type is {schema.Type}. "
            + $"Put [{attribute}] only on {allowed}.");

    private static InvalidOperationException NoValuePasses(JsonPropertyInfo property, string minimum, string maximum) =>
        new($"{property.DeclaringType.Name}.{property.Name} has the minimum {minimum} and the maximum {maximum}, so no value passes. "
            + "Give [SchemaMinimum] a value that is not more than the value of [SchemaMaximum].");
}
