namespace FullstackTemplate.Server.Resources.Entrypoints;

using System.Text.Json;
using System.Text.Json.Serialization;
using Mcp;

/// <summary>
/// The types that the entrypoints serialize themselves. The endpoint DTOs are not in it, so they use the reflection
/// fallback until the template aims at native AOT (see <c>docs/backend/source-generators.md</c>).
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(PaginationMetadata))]
[JsonSerializable(typeof(McpToolError))]
internal sealed partial class EntrypointJsonContext : JsonSerializerContext;
