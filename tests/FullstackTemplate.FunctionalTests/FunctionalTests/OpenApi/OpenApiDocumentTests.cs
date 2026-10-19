namespace FullstackTemplate.FunctionalTests.FunctionalTests.OpenApi;

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Shouldly;

// Each test checks one rule of the document
public class OpenApiDocumentTests
{
    // GET /api/v1/weather is the only operation that allows anonymous callers
    public const string AnonymousOperation = "GET /api/v1/weather";

    [Fact]
    public async Task success_responses_and_request_bodies_use_only_json()
    {
        var root = await LoadDocumentAsync();

        var successContents = Operations(root)
            .SelectMany(o => Responses(o.Operation)
                .Where(r => r.Status[0] is '2' && r.Response["content"] is not null)
                .Select(r => (Name: $"{o.Name} {r.Status}", Content: r.Response["content"]!.AsObject())));
        var requestContents = Operations(root)
            .Where(o => o.Operation["requestBody"]?["content"] is not null)
            .Select(o => (Name: $"{o.Name} request body", Content: o.Operation["requestBody"]!["content"]!.AsObject()));
        var contents = successContents.Concat(requestContents).ToList();

        contents.ShouldNotBeEmpty();
        foreach (var (name, content) in contents)
            content.Select(c => c.Key).ShouldBe(["application/json"], name);
    }

    [Fact]
    public async Task numbers_are_typed_only_as_numbers()
    {
        var root = await LoadDocumentAsync();

        var numberSchemas = Descendants(root)
            .OfType<JsonObject>()
            .Where(node => TypesOf(node).Any(type => type is "integer" or "number"))
            .ToList();

        numberSchemas.ShouldNotBeEmpty();
        foreach (var schema in numberSchemas)
        {
            TypesOf(schema).ShouldNotContain("string", schema.ToJsonString());
            schema.ContainsKey("pattern").ShouldBeFalse(schema.ToJsonString());
        }
    }

    [Fact]
    public async Task only_secured_operations_declare_unauthorized()
    {
        var root = await LoadDocumentAsync();

        var operations = Operations(root).ToList();

        operations.Select(o => o.Name).ShouldContain(AnonymousOperation);
        foreach (var (name, _, operation) in operations)
        {
            var unauthorized = operation["responses"]?["401"];
            if (name == AnonymousOperation)
            {
                unauthorized.ShouldBeNull(name);
                continue;
            }

            unauthorized.ShouldNotBeNull(name);
            unauthorized["content"].ShouldBeNull(name);
        }
    }

    /// <summary>
    /// The v1 document, with every registered transformer applied.
    /// </summary>
    public static async Task<JsonNode> LoadDocumentAsync()
    {
        // The test host does not run in Development, so it does not map /openapi/v1.json.
        // IOpenApiDocumentProvider is the service behind that endpoint. It runs every registered transformer.
        var provider = TestingWebApplicationFactory.Instance.Services
            .GetRequiredKeyedService<IOpenApiDocumentProvider>("v1");
        var document = await provider.GetOpenApiDocumentAsync();
        var json = await document.SerializeAsJsonAsync(OpenApiSpecVersion.OpenApi3_1);
        return JsonNode.Parse(json).ShouldNotBeNull();
    }

    public static IEnumerable<(string Name, string Method, JsonNode Operation)> Operations(JsonNode root) =>
        root["paths"]!.AsObject()
            .SelectMany(path => path.Value!.AsObject()
                .Where(operation => operation.Value?["responses"] is not null)
                .Select(operation => ($"{operation.Key.ToUpperInvariant()} {path.Key}", operation.Key, operation.Value!)));

    private static IEnumerable<(string Status, JsonNode Response)> Responses(JsonNode operation) =>
        operation["responses"]!.AsObject().Select(response => (response.Key, response.Value!));

    // A schema type is a single name or a list of names. A nullable schema can also be a oneOf with a null type.
    private static List<string> TypesOf(JsonNode schema)
    {
        var types = schema["type"] switch
        {
            JsonArray array => array.Select(item => item!.GetValue<string>()).ToList(),
            JsonValue value when value.GetValueKind() == JsonValueKind.String => [value.GetValue<string>()],
            _ => [],
        };

        if (schema["oneOf"] is JsonArray oneOf)
            types.AddRange(oneOf.SelectMany(option => TypesOf(option!)));

        return types;
    }

    private static IEnumerable<JsonNode> Descendants(JsonNode node)
    {
        yield return node;
        var children = node switch
        {
            JsonObject obj => obj.Select(child => child.Value),
            JsonArray array => array.AsEnumerable(),
            _ => [],
        };
        foreach (var child in children.OfType<JsonNode>())
        foreach (var descendant in Descendants(child))
            yield return descendant;
    }
}
