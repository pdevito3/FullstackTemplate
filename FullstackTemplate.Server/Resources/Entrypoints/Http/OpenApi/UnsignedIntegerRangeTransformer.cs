namespace FullstackTemplate.Server.Resources.Entrypoints.Http.OpenApi;

using System.Globalization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

/// <summary>
/// Gives a <c>uint</c> the range of a <c>uint</c> in place of the format <c>uint32</c>. OpenAPI does not define that
/// format, so a client generator such as Kubb warns and drops it. The range keeps the contract and every generator reads it.
/// </summary>
public sealed class UnsignedIntegerRangeTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (schema.Format != "uint32")
            return Task.CompletedTask;

        schema.Format = null;
        schema.Minimum = uint.MinValue.ToString(CultureInfo.InvariantCulture);
        schema.Maximum = uint.MaxValue.ToString(CultureInfo.InvariantCulture);

        return Task.CompletedTask;
    }
}
