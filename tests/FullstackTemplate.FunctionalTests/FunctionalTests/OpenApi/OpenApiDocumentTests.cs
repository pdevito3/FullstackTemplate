namespace FullstackTemplate.FunctionalTests.FunctionalTests.OpenApi;

using System.Text.Json;
using System.Text.Json.Nodes;
using FullstackTemplate.Server.Domain.Agents;
using FullstackTemplate.Server.Domain.EmailAddresses;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Users;
// example:start
using FullstackTemplate.Server.Domain.TimeZones;
using FullstackTemplate.Server.Examples.Venues;
using FullstackTemplate.Server.Examples.Workshops;
// example:end
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Shouldly;

// Each test checks one rule of the document
public class OpenApiDocumentTests
{
    private const string ValidationProblemRef = "#/components/schemas/ValidationProblemDetails";

    public static readonly string[] AnonymousOperations =
    [
        // example:start
        "GET /api/v1/weather",
        // example:end
    ];

    [Test]
    public async Task tenant_list_documents_the_pagination_header()
    {
        // Arrange
        var root = await LoadDocumentAsync();

        // Act
        var header = root["paths"]?["/api/v1/tenants"]?["get"]?["responses"]?["200"]?["headers"]?["X-Pagination"];

        // Assert
        header.ShouldNotBeNull();
        header["content"]?["application/json"]?["schema"]?["$ref"]?.GetValue<string>()
            .ShouldBe("#/components/schemas/PaginationMetadata");
    }

    [Test]
    public async Task error_responses_use_only_problem_json()
    {
        // Arrange
        var root = await LoadDocumentAsync();

        // Act
        var errorResponses = Operations(root)
            .SelectMany(o => Responses(o.Operation)
                .Where(r => r.Status[0] is '4' or '5' && r.Response["content"] is not null)
                .Select(r => (Name: $"{o.Name} {r.Status}", Content: r.Response["content"]!.AsObject())))
            .ToList();

        // Assert
        errorResponses.ShouldNotBeEmpty();
        foreach (var (name, content) in errorResponses)
            content.Select(c => c.Key).ShouldBe(["application/problem+json"], name);
    }

    [Test]
    public async Task success_responses_and_request_bodies_use_only_json()
    {
        // Arrange
        var root = await LoadDocumentAsync();

        // Act
        var successContents = Operations(root)
            .SelectMany(o => Responses(o.Operation)
                .Where(r => r.Status[0] is '2' && r.Response["content"] is not null)
                .Select(r => (Name: $"{o.Name} {r.Status}", Content: r.Response["content"]!.AsObject())));
        var requestContents = Operations(root)
            .Where(o => o.Operation["requestBody"]?["content"] is not null)
            .Select(o => (Name: $"{o.Name} request body", Content: o.Operation["requestBody"]!["content"]!.AsObject()));
        var contents = successContents.Concat(requestContents).ToList();

        // Assert
        contents.ShouldNotBeEmpty();
        foreach (var (name, content) in contents)
            content.Select(c => c.Key).ShouldBe(["application/json"], name);
    }

    // The keys of the errors of the problem are camelCase, so the parameter names must be camelCase too
    [Test]
    public async Task parameter_names_are_camel_case()
    {
        // Arrange
        var root = await LoadDocumentAsync();

        // Act
        var parameters = Operations(root)
            .SelectMany(o => (o.Operation["parameters"]?.AsArray() ?? [])
                .Select(p => (Operation: o.Name, Name: p!["name"]!.GetValue<string>())))
            .ToList();

        // Assert
        parameters.ShouldNotBeEmpty();
        foreach (var (operation, name) in parameters)
            char.IsLower(name[0]).ShouldBeTrue($"{operation} {name}");
    }

    [Test]
    public async Task numbers_are_typed_only_as_numbers()
    {
        // Arrange
        var root = await LoadDocumentAsync();

        // Act
        var numberSchemas = Descendants(root)
            .OfType<JsonObject>()
            .Where(node => TypesOf(node).Any(type => type is "integer" or "number"))
            .ToList();

        // Assert
        numberSchemas.ShouldNotBeEmpty();
        foreach (var schema in numberSchemas)
        {
            TypesOf(schema).ShouldNotContain("string", schema.ToJsonString());
            schema.ContainsKey("pattern").ShouldBeFalse(schema.ToJsonString());
        }
    }

    [Test]
    public async Task number_formats_are_formats_that_openapi_defines()
    {
        // Arrange
        var root = await LoadDocumentAsync();

        // Act
        var formats = Descendants(root)
            .OfType<JsonObject>()
            .Where(node => TypesOf(node).Any(type => type is "integer" or "number") && node.ContainsKey("format"))
            .Select(node => node["format"]!.GetValue<string>())
            .Distinct();

        // Assert
        formats.ShouldBeSubsetOf(["int32", "int64", "float", "double"]);
    }

    [Test]
    public async Task non_nullable_dto_properties_are_required()
    {
        // Arrange
        var root = await LoadDocumentAsync();

        // Act
        var dtoSchemas = root["components"]!["schemas"]!.AsObject()
            .Where(schema => schema.Key.EndsWith("Dto", StringComparison.Ordinal))
            .ToList();

        // Assert
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

    [Test]
    public async Task dto_strings_that_hold_a_value_object_name_list_the_names()
    {
        // Arrange
        var root = await LoadDocumentAsync();
        var schemas = root["components"]!["schemas"]!;

        JsonNode? Property(string schema, string property) => schemas[schema]?["properties"]?[property];
        (string Name, JsonNode? Schema, List<string> Names)[] expectations =
        [
            ("AgentDto.mode", Property("AgentDto", "mode"), AgentMode.ListNames()),
            ("AgentDto.permissions items", Property("AgentDto", "permissions")?["items"], Permission.ListNames()),
            ("AgentForCreationDto.mode", Property("AgentForCreationDto", "mode"), AgentMode.ListNames()),
            ("AgentForCreationDto.permissions items", Property("AgentForCreationDto", "permissions")?["items"],
                Permission.ListNames()),
            ("AgentPermissionDto.permission", Property("AgentPermissionDto", "permission"), Permission.ListNames()),
            ("UserDto.role", Property("UserDto", "role"), UserRole.ListNames()),
            ("UserDto.permissions items", Property("UserDto", "permissions")?["items"], Permission.ListNames()),
            ("UserForCreationDto.role", Property("UserForCreationDto", "role"), UserRole.ListNames()),
            ("InitiateUserAndTenantDto.role", Property("InitiateUserAndTenantDto", "role"), UserRole.ListNames()),
            ("UpdateUserRoleDto.role", Property("UpdateUserRoleDto", "role"), UserRole.ListNames()),
            ("UserPermissionDto.permission", Property("UserPermissionDto", "permission"), Permission.ListNames()),
        ];

        // Act & Assert
        expectations.Select(e => $"{e.Name}: {string.Join(", ", EnumOf(e.Schema))}")
            .ShouldBe(expectations.Select(e => $"{e.Name}: {string.Join(", ", e.Names)}"));
    }

    [Test]
    public async Task request_strings_with_a_domain_text_limit_have_its_max_length()
    {
        // Arrange
        var root = await LoadDocumentAsync();
        var schemas = root["components"]!["schemas"]!;

        (string Name, int MaxLength)[] expectations =
        [
            ("AgentForCreationDto.name", Agent.NameMaxLength),
            ("AgentForCreationDto.clientId", Agent.ClientIdMaxLength),
            ("TenantForCreationDto.name", Tenant.NameMaxLength),
            ("TenantForUpdateDto.name", Tenant.NameMaxLength),
            ("UserForCreationDto.firstName", User.FirstNameMaxLength),
            ("UserForCreationDto.lastName", User.LastNameMaxLength),
            ("UserForCreationDto.identifier", User.IdentifierMaxLength),
            ("UserForCreationDto.email", EmailAddress.MaxLength),
            ("UserForCreationDto.username", User.UsernameMaxLength),
            ("UserForUpdateDto.firstName", User.FirstNameMaxLength),
            ("UserForUpdateDto.lastName", User.LastNameMaxLength),
            ("UserForUpdateDto.email", EmailAddress.MaxLength),
            ("UserForUpdateDto.username", User.UsernameMaxLength),
            ("InitiateUserAndTenantDto.tenantName", Tenant.NameMaxLength),
            ("InitiateUserAndTenantDto.firstName", User.FirstNameMaxLength),
            ("InitiateUserAndTenantDto.lastName", User.LastNameMaxLength),
            ("InitiateUserAndTenantDto.identifier", User.IdentifierMaxLength),
            ("InitiateUserAndTenantDto.email", EmailAddress.MaxLength),
            ("InitiateUserAndTenantDto.username", User.UsernameMaxLength),
            // example:start
            ("VenueForCreationDto.name", Venue.NameMaxLength),
            ("VenueForCreationDto.timeZone", TimeZoneId.MaxLength),
            ("WorkshopForCreationDto.title", Workshop.TitleMaxLength),
            ("WorkshopForUpdateDto.title", Workshop.TitleMaxLength),
            // example:end
        ];

        int? MaxLengthOf(string name)
        {
            var parts = name.Split('.');
            return schemas[parts[0]]?["properties"]?[parts[1]]?["maxLength"]?.GetValue<int>();
        }

        // Act & Assert
        expectations.Select(e => $"{e.Name}: {MaxLengthOf(e.Name)}")
            .ShouldBe(expectations.Select(e => $"{e.Name}: {e.MaxLength}"));
    }

    [Test]
    public async Task request_numbers_with_a_domain_range_have_its_minimum_and_maximum()
    {
        // Arrange
        var root = await LoadDocumentAsync();
        var schemas = root["components"]!["schemas"]!;

        (string Name, int Minimum, int Maximum)[] expectations =
        [
            // example:start
            ("WorkshopForCreationDto.capacity", Workshop.CapacityMinimum, Workshop.CapacityMaximum),
            ("WorkshopForUpdateDto.capacity", Workshop.CapacityMinimum, Workshop.CapacityMaximum),
            // example:end
        ];

        string RangeOf(string name)
        {
            var parts = name.Split('.');
            var property = schemas[parts[0]]?["properties"]?[parts[1]];
            return $"{property?["minimum"]?.ToJsonString()} to {property?["maximum"]?.ToJsonString()}";
        }

        // Act & Assert
        expectations.Select(e => $"{e.Name}: {RangeOf(e.Name)}")
            .ShouldBe(expectations.Select(e => $"{e.Name}: {e.Minimum} to {e.Maximum}"));
    }

    [Test]
    public async Task each_request_string_has_a_max_length_or_a_list_of_names()
    {
        // Arrange
        var root = await LoadDocumentAsync();
        var schemas = root["components"]!["schemas"]!;
        // An entity ID is not text that a person types. The client sends an ID that it read from the API.
        string[] entityIds =
        [
            "AgentForCreationDto.tenantId",
            "UserForCreationDto.tenantId",
            // example:start
            "WorkshopForCreationDto.venueId",
            // example:end
        ];

        static IEnumerable<string> RefsUnder(JsonNode node) =>
            Descendants(node).OfType<JsonObject>().Select(child => child["$ref"]?.GetValue<string>()).OfType<string>();
        static bool IsFreeText(JsonNode schema) =>
            TypesOf(schema).Contains("string") && schema["maxLength"] is null && schema["enum"] is null
            && schema["format"]?.GetValue<string>() != "date-time";

        var requestSchemas = new HashSet<string>();
        var pending = new Queue<string>(Operations(root)
            .Select(o => o.Operation["requestBody"])
            .OfType<JsonNode>()
            .SelectMany(RefsUnder));
        while (pending.TryDequeue(out var schemaRef))
        {
            var name = schemaRef.Split('/')[^1];
            if (!requestSchemas.Add(name))
                continue;
            foreach (var nested in RefsUnder(schemas[name]!))
                pending.Enqueue(nested);
        }

        // Act
        var unlimited = requestSchemas
            .SelectMany(name => (schemas[name]!["properties"]?.AsObject() ?? [])
                .Where(property => IsFreeText(property.Value!)
                    || (TypesOf(property.Value!).Contains("array") && property.Value!["items"] is { } items
                        && IsFreeText(items)))
                .Select(property => $"{name}.{property.Key}"))
            .Except(entityIds)
            .Order()
            .ToList();

        // Assert
        requestSchemas.ShouldContain("UserForCreationDto");
        entityIds.Where(id => schemas[id.Split('.')[0]]?["properties"]?[id.Split('.')[1]] is null)
            .ShouldBeEmpty("Remove each entity ID that the document no longer has from the exception list.");
        unlimited.ShouldBeEmpty("Give each string field of a request DTO [SchemaMaxLength] with the domain constant that "
            + "its entity validation checks, or [NamesOf<T>].");
    }

    [Test]
    public async Task each_write_declares_a_validation_problem()
    {
        // Arrange
        var root = await LoadDocumentAsync();

        // Act
        var writes = Operations(root).Where(o => o.Method != "get").ToList();

        // Assert
        writes.ShouldNotBeEmpty();
        foreach (var (name, _, operation) in writes)
        {
            var schemaRef = operation["responses"]?["422"]?["content"]?["application/problem+json"]?["schema"]?["$ref"]?
                .GetValue<string>();
            schemaRef.ShouldBe(ValidationProblemRef, name);
        }
    }

    [Test]
    public async Task only_secured_operations_declare_unauthorized()
    {
        // Arrange
        var root = await LoadDocumentAsync();

        // Act
        var operations = Operations(root).ToList();

        // Assert
        AnonymousOperations.ShouldBeSubsetOf(operations.Select(o => o.Name));
        foreach (var (name, _, operation) in operations)
        {
            var unauthorized = operation["responses"]?["401"];
            if (AnonymousOperations.Contains(name))
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

    private static List<string> EnumOf(JsonNode? schema) =>
        schema?["enum"]?.AsArray().Select(item => item!.GetValue<string>()).ToList() ?? [];

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
