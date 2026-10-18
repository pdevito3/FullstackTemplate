namespace FullstackTemplate.FunctionalTests.FunctionalTests.OpenApi;

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Shouldly;

public class OpenApiDocumentTests
{
    private const string ValidationProblemRef = "#/components/schemas/ValidationProblemDetails";

    [Fact]
    public async Task document_states_the_contract_that_the_transformers_set()
    {
        // Arrange
        // The test host does not run in Development, so it does not map /openapi/v1.json.
        // IOpenApiDocumentProvider is the service behind that endpoint. It runs every registered transformer.
        var provider = TestingWebApplicationFactory.Instance.Services
            .GetRequiredKeyedService<IOpenApiDocumentProvider>("v1");

        // Act
        var document = await provider.GetOpenApiDocumentAsync();
        var json = await document.SerializeAsJsonAsync(OpenApiSpecVersion.OpenApi3_1);
        var root = JsonNode.Parse(json).ShouldNotBeNull();

        // Assert
        ShouldHavePaginationHeaderOnTenantList(root);
        ShouldUseProblemJsonForErrorResponses(root);
        ShouldTypeNumbersOnlyAsNumbers(root);
        ShouldRequireTheNonNullableDtoProperties(root);
        ShouldDeclareValidationProblemOnEachWrite(root);
        ShouldDeclareUnauthorizedOnlyOnSecuredOperations(root);
    }

    private static void ShouldHavePaginationHeaderOnTenantList(JsonNode root)
    {
        var header = root["paths"]?["/api/v1/tenants"]?["get"]?["responses"]?["200"]?["headers"]?["X-Pagination"];
        header.ShouldNotBeNull();
        header["content"]?["application/json"]?["schema"]?["$ref"]?.GetValue<string>()
            .ShouldBe("#/components/schemas/PaginationMetadata");
    }

    private static void ShouldUseProblemJsonForErrorResponses(JsonNode root)
    {
        var errorResponses = Operations(root)
            .SelectMany(o => Responses(o.Operation)
                .Where(r => r.Status[0] is '4' or '5' && r.Response["content"] is not null)
                .Select(r => (Name: $"{o.Name} {r.Status}", Content: r.Response["content"]!.AsObject())))
            .ToList();

        errorResponses.ShouldNotBeEmpty();
        foreach (var (name, content) in errorResponses)
            content.Select(c => c.Key).ShouldBe(["application/problem+json"], name);
    }

    private static void ShouldTypeNumbersOnlyAsNumbers(JsonNode root)
    {
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

    private static void ShouldRequireTheNonNullableDtoProperties(JsonNode root)
    {
        var dtoSchemas = root["components"]!["schemas"]!.AsObject()
            .Where(schema => schema.Key.EndsWith("Dto", StringComparison.Ordinal))
            .ToList();

        dtoSchemas.Select(schema => schema.Key).ShouldContain("UserForCreationDto");
        foreach (var (name, schema) in dtoSchemas)
        {
            var nonNullable = schema!["properties"]!.AsObject()
                .Where(property => !TypesOf(property.Value!).Contains("null"))
                .Select(property => property.Key);
            var required = schema["required"]?.AsArray().Select(item => item!.GetValue<string>()) ?? [];

            required.ShouldBe(nonNullable, ignoreOrder: true, name);
        }

        root["components"]!["schemas"]!["UserForCreationDto"]!["required"]!.AsArray()
            .Select(item => item!.GetValue<string>())
            .ShouldContain("firstName");
    }

    private static void ShouldDeclareValidationProblemOnEachWrite(JsonNode root)
    {
        var writes = Operations(root).Where(o => o.Method != "get").ToList();

        writes.ShouldNotBeEmpty();
        foreach (var (name, _, operation) in writes)
        {
            var schemaRef = operation["responses"]?["422"]?["content"]?["application/problem+json"]?["schema"]?["$ref"]?
                .GetValue<string>();
            schemaRef.ShouldBe(ValidationProblemRef, name);
        }
    }

    private static void ShouldDeclareUnauthorizedOnlyOnSecuredOperations(JsonNode root)
    {
        // GET /api/v1/weather is the only operation that allows anonymous callers
        const string anonymous = "GET /api/v1/weather";
        var operations = Operations(root).ToList();

        operations.Select(o => o.Name).ShouldContain(anonymous);
        foreach (var (name, _, operation) in operations)
        {
            var unauthorized = operation["responses"]?["401"];
            if (name == anonymous)
            {
                unauthorized.ShouldBeNull(name);
                continue;
            }

            unauthorized.ShouldNotBeNull(name);
            unauthorized["content"].ShouldBeNull(name);
        }
    }

    private static IEnumerable<(string Name, string Method, JsonNode Operation)> Operations(JsonNode root) =>
        root["paths"]!.AsObject()
            .SelectMany(path => path.Value!.AsObject()
                .Where(operation => operation.Value?["responses"] is not null)
                .Select(operation => ($"{operation.Key.ToUpperInvariant()} {path.Key}", operation.Key, operation.Value!)));

    private static IEnumerable<(string Status, JsonNode Response)> Responses(JsonNode operation) =>
        operation["responses"]!.AsObject().Select(response => (response.Key, response.Value!));

    // A schema type is a single name or a list of names. A nullable schema can also be a oneOf with a null type.
    private static IReadOnlyList<string> TypesOf(JsonNode schema)
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
