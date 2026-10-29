namespace FullstackTemplate.UnitTests.Resources.Entrypoints;

using System.Text.Json;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.Server.Resources.Entrypoints.Mcp;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Npgsql;
using NSubstitute;
using Shouldly;

public class McpDomainErrorsTests
{
    private const string StaleMessage = "This record changed after you loaded it. Reload it, then make your change again.";

    [Test]
    public async Task a_stale_version_is_an_aborted_tool_error_with_the_message_under_the_version()
    {
        // Act
        var result = await CallToolThatThrows(new StaleVersionException());

        // Assert
        result.IsError.ShouldBe(true);
        result.Content.OfType<TextContentBlock>().ShouldHaveSingleItem().Text.ShouldBe($"aborted: {StaleMessage}");
        var content = result.StructuredContent.ShouldNotBeNull();
        content.GetProperty("code").GetString().ShouldBe("aborted");
        content.GetProperty("title").GetString().ShouldBe("The record changed after you loaded it.");
        content.GetProperty("errors").GetProperty("version")[0].GetString().ShouldBe(StaleMessage);
    }

    [Test]
    public async Task a_concurrent_change_is_an_aborted_tool_error_with_its_own_title()
    {
        // Act
        var result = await CallToolThatThrows(new ConcurrentChangeException());

        // Assert
        result.IsError.ShouldBe(true);
        result.Content.OfType<TextContentBlock>().ShouldHaveSingleItem().Text.ShouldBe(
            "aborted: Other users changed this record at the same time, so the change did not save. Send it again.");
        var content = result.StructuredContent.ShouldNotBeNull();
        content.GetProperty("code").GetString().ShouldBe("aborted");
        content.GetProperty("title").GetString().ShouldBe("Other users changed the record at the same time.");
    }

    [Test]
    public async Task a_conflict_is_an_already_exists_tool_error()
    {
        // Act
        var result = await CallToolThatThrows(new ConflictException("name", "A tenant with this name already exists."));

        // Assert
        result.IsError.ShouldBe(true);
        result.Content.OfType<TextContentBlock>().ShouldHaveSingleItem().Text
            .ShouldBe("already_exists: A tenant with this name already exists.");
        var content = result.StructuredContent.ShouldNotBeNull();
        content.GetProperty("code").GetString().ShouldBe("already_exists");
        content.GetProperty("title").GetString().ShouldBe("The request conflicts with a record that exists.");
    }

    [Test]
    public async Task a_save_that_a_unique_index_stops_has_the_message_of_the_check_on_the_field()
    {
        // Arrange
        var conflict = DatabaseConflict.From(new PostgresException(
            "duplicate key", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation, constraintName: "ix_tenants_name"),
            new AppDbContextFactory().CreateDbContext([]).Model)!;

        // Act
        var result = await CallToolThatThrows(conflict);

        // Assert
        var content = result.StructuredContent.ShouldNotBeNull();
        content.GetProperty("code").GetString().ShouldBe("already_exists");
        content.GetProperty("detail").GetString().ShouldBe("A tenant with this name already exists.");
        content.GetProperty("errors").GetProperty("name")[0].GetString().ShouldBe("A tenant with this name already exists.");
    }

    [Test]
    public async Task a_kind_with_no_field_errors_has_no_errors_property()
    {
        // Act
        var result = await CallToolThatThrows(new NotFoundException("The tenant was not found."));

        // Assert
        var content = result.StructuredContent.ShouldNotBeNull();
        content.GetProperty("code").GetString().ShouldBe("not_found");
        content.GetProperty("detail").GetString().ShouldBe("The tenant was not found.");
        content.TryGetProperty("errors", out _).ShouldBeFalse();
    }

    [Test]
    public async Task a_json_error_with_no_argument_of_the_wrong_type_stays_a_server_fault()
    {
        // Act
        var call = () => CallToolThatThrows(new JsonException("The tool could not read its own data."));

        // Assert
        await call.ShouldThrowAsync<JsonException>();
    }

    [Test]
    public async Task a_json_error_when_each_argument_converts_stays_a_server_fault()
    {
        // Act
        var call = () => CallListTool("""{ "pageSize": 10 }""");

        // Assert
        await call.ShouldThrowAsync<JsonException>();
    }

    [Test]
    public async Task an_argument_named_for_a_parameter_that_the_sdk_fills_is_not_checked()
    {
        // Act
        var result = await CallListTool("""{ "pageSize": "ten", "cancellationToken": 1 }""");

        // Assert
        result.IsError.ShouldBe(true);
        var errors = result.StructuredContent.ShouldNotBeNull().GetProperty("errors");
        errors.EnumerateObject().Select(e => e.Name).ShouldBe(["pageSize"]);
        errors.GetProperty("pageSize")[0].GetString().ShouldBe("The value 'ten' is not valid for pageSize.");
    }

    [Test]
    public async Task a_long_value_of_the_wrong_type_is_cut_in_the_message()
    {
        // Act
        var result = await CallListTool($$"""{ "pageSize": "{{new string('x', 150)}}" }""");

        // Assert
        var errors = result.StructuredContent.ShouldNotBeNull().GetProperty("errors");
        errors.GetProperty("pageSize")[0].GetString().ShouldBe($"The value '{new string('x', 100)}...' is not valid for pageSize.");
    }

    // The tool throws the JsonException that the SDK throws when an argument does not convert
    private static async Task<CallToolResult> CallListTool(string arguments)
    {
        var handler = McpDomainErrors.Filter((_, _) => throw new JsonException("The JSON value could not be converted."));
        var request = new RequestContext<CallToolRequestParams>(
            Substitute.For<McpServer>(),
            new JsonRpcRequest { Method = RequestMethods.ToolsCall },
            new CallToolRequestParams
            {
                Name = "list_items",
                Arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(arguments),
            })
        {
            MatchedPrimitive = McpServerTool.Create(ListItems, new() { Name = "list_items" }),
        };
        return await handler(request, CancellationToken.None);
    }

    private static string ListItems(int? pageSize, CancellationToken cancellationToken) => $"{pageSize}";

    private static async Task<CallToolResult> CallToolThatThrows(Exception exception)
    {
        var handler = McpDomainErrors.Filter((_, _) => throw exception);
        var request = new RequestContext<CallToolRequestParams>(
            Substitute.For<McpServer>(),
            new JsonRpcRequest { Method = RequestMethods.ToolsCall },
            new CallToolRequestParams { Name = "add_tenant" });
        return await handler(request, CancellationToken.None);
    }
}
