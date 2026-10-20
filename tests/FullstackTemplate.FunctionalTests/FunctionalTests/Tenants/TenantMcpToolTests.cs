namespace FullstackTemplate.FunctionalTests.FunctionalTests.Tenants;

using System.Net;
using System.Text;
using System.Text.Json;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Resources.Entrypoints;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Shouldly;

/// <summary>
/// The MCP entrypoint sends the same MediatR requests as HTTP, so it has the same tenant scope and the
/// same access rules. These tests check the transport: auth, the scope, and the tool error of each domain error.
/// </summary>
public class TenantMcpToolTests : TestBase
{
    [Test]
    public async Task tools_list_has_the_tenant_tools()
    {
        // Arrange
        var (_, user) = await AddTenantWithUserAsync();
        SignInAs(user);
        await using var client = await CreateMcpClientAsync();

        // Act
        var tools = await client.ListToolsAsync();

        // Assert
        tools.Select(t => t.Name).ShouldBe(["add_tenant", "get_tenant", "list_tenants"], ignoreOrder: true);
    }

    [Test]
    public async Task user_can_get_their_own_tenant()
    {
        // Arrange
        var (tenant, user) = await AddTenantWithUserAsync();
        SignInAs(user);
        await using var client = await CreateMcpClientAsync();

        // Act
        var result = await client.CallToolAsync("get_tenant", new Dictionary<string, object?> { ["id"] = tenant.Id });

        // Assert
        result.IsError.ShouldNotBe(true);
        var content = result.StructuredContent.ShouldNotBeNull();
        content.GetProperty("id").GetString().ShouldBe(tenant.Id);
        content.GetProperty("name").GetString().ShouldBe(tenant.Name);
    }

    [Test]
    public async Task user_cannot_get_another_tenant()
    {
        // Arrange
        var (_, user) = await AddTenantWithUserAsync();
        var (otherTenant, _) = await AddTenantWithUserAsync();
        SignInAs(user);
        await using var client = await CreateMcpClientAsync();

        // Act
        var result = await client.CallToolAsync("get_tenant", new Dictionary<string, object?> { ["id"] = otherTenant.Id });

        // Assert
        ShouldBeToolError(result, "not_found");
        result.StructuredContent.ShouldNotBeNull().TryGetProperty("errors", out _).ShouldBeFalse();
    }

    [Test]
    public async Task user_sees_only_their_own_tenant_in_the_list()
    {
        // Arrange
        var (tenant, user) = await AddTenantWithUserAsync();
        await AddTenantWithUserAsync();
        SignInAs(user);
        await using var client = await CreateMcpClientAsync();

        // Act
        var result = await client.CallToolAsync("list_tenants", new Dictionary<string, object?> { ["pageSize"] = 500 });

        // Assert
        result.IsError.ShouldNotBe(true);
        var content = result.StructuredContent.ShouldNotBeNull();
        content.GetProperty("items").EnumerateArray().ShouldHaveSingleItem().GetProperty("id").GetString().ShouldBe(tenant.Id);
        content.GetProperty("pagination").GetProperty("totalCount").GetInt32().ShouldBe(1);
    }

    [Test]
    public async Task page_out_of_range_is_an_invalid_argument()
    {
        // Arrange
        var (_, user) = await AddTenantWithUserAsync();
        SignInAs(user);
        await using var client = await CreateMcpClientAsync();

        // Act
        var result = await client.CallToolAsync("list_tenants", new Dictionary<string, object?> { ["pageNumber"] = 0 });

        // Assert
        ShouldBeToolError(result, "invalid_argument");
        result.StructuredContent.ShouldNotBeNull().GetProperty("errors").TryGetProperty("pageNumber", out _).ShouldBeTrue();
    }

    [Test]
    public async Task list_with_no_arguments_gives_the_first_page_with_the_default_size()
    {
        // Arrange
        var (_, user) = await AddTenantWithUserAsync();
        SignInAs(user);
        await using var client = await CreateMcpClientAsync();

        // Act
        var result = await client.CallToolAsync("list_tenants", new Dictionary<string, object?>());

        // Assert
        result.IsError.ShouldNotBe(true);
        var pagination = result.StructuredContent.ShouldNotBeNull().GetProperty("pagination");
        pagination.GetProperty("pageNumber").GetInt32().ShouldBe(1);
        pagination.GetProperty("pageSize").GetInt32().ShouldBe(FullstackTemplate.Server.Resources.BasePaginationParameters.DefaultPageSizeValue);
    }

    [Test]
    public async Task empty_tenant_name_is_an_invalid_argument_with_the_field()
    {
        // Arrange
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object>
        {
            ["client_id"] = TestingWebApplicationFactory.TrustedMachineClientId
        });
        await using var client = await CreateMcpClientAsync();

        // Act
        var result = await client.CallToolAsync("add_tenant", new Dictionary<string, object?> { ["name"] = "" });

        // Assert
        ShouldBeToolError(result, "invalid_argument");
        result.StructuredContent.ShouldNotBeNull().GetProperty("errors").GetProperty("name")[0].GetString()
            .ShouldBe("Please provide a tenant name.");
    }

    [Test]
    public async Task missing_tenant_name_is_an_invalid_argument_with_the_field()
    {
        // Arrange
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object>
        {
            ["client_id"] = TestingWebApplicationFactory.TrustedMachineClientId
        });
        await using var client = await CreateMcpClientAsync();

        // Act
        var result = await client.CallToolAsync("add_tenant", new Dictionary<string, object?>());

        // Assert
        ShouldBeToolError(result, "invalid_argument");
        // The message is the same as the message for a missing body field
        result.StructuredContent.ShouldNotBeNull().GetProperty("errors").GetProperty("name")[0].GetString()
            .ShouldBe("The name field is required.");
    }

    [Test]
    public async Task user_cannot_add_a_tenant()
    {
        // Arrange
        var (_, user) = await AddTenantWithUserAsync();
        SignInAs(user);
        await using var client = await CreateMcpClientAsync();

        // Act
        var result = await client.CallToolAsync("add_tenant", new Dictionary<string, object?> { ["name"] = $"Tenant {Guid.NewGuid()}" });

        // Assert
        ShouldBeToolError(result, "permission_denied");
    }

    [Test]
    public async Task trusted_machine_client_can_add_a_tenant_once()
    {
        // Arrange
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object>
        {
            ["client_id"] = TestingWebApplicationFactory.TrustedMachineClientId
        });
        await using var client = await CreateMcpClientAsync();
        var arguments = new Dictionary<string, object?> { ["name"] = $"Tenant {Guid.NewGuid()}" };

        // Act
        var added = await client.CallToolAsync("add_tenant", arguments);
        var duplicate = await client.CallToolAsync("add_tenant", arguments);

        // Assert
        added.IsError.ShouldNotBe(true);
        var id = added.StructuredContent.ShouldNotBeNull().GetProperty("id").GetString().ShouldNotBeNull();
        (await FindAsync<Tenant>(id)).ShouldNotBeNull();
        ShouldBeToolError(duplicate, "already_exists");
        duplicate.StructuredContent.ShouldNotBeNull().GetProperty("errors").GetProperty("name")[0].GetString()
            .ShouldBe("A tenant with this name already exists.");
    }

    /// <summary>
    /// The transport must stay stateless. A stateful session runs a tool call outside the HTTP request of the call,
    /// so the tool would not see the caller or the tenant scope of that request.
    /// </summary>
    [Test]
    public async Task the_transport_is_stateless_and_gives_no_session()
    {
        // Arrange
        var (_, user) = await AddTenantWithUserAsync();
        SignInAs(user);
        using var content = new StringContent(
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}""",
            Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, EntrypointRegistration.McpPath) { Content = content };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");

        // Act
        var result = await FactoryClient.SendAsync(request);
        var options = await ExecuteScopeAsync(services =>
            Task.FromResult(services.GetRequiredService<IOptions<HttpServerTransportOptions>>().Value));

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.Headers.Contains("Mcp-Session-Id").ShouldBeFalse();
        options.Stateless.ShouldBeTrue();
    }

    [Test]
    public async Task request_with_no_token_is_unauthorized()
    {
        // Arrange
        using var content = new StringContent(
            """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""", Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, EntrypointRegistration.McpPath) { Content = content };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");

        // Act
        var result = await FactoryClient.SendAsync(request);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static void ShouldBeToolError(CallToolResult result, string code)
    {
        result.IsError.ShouldBe(true);
        result.StructuredContent.ShouldNotBeNull().GetProperty("code").GetString().ShouldBe(code);
        result.Content.OfType<TextContentBlock>().ShouldHaveSingleItem().Text.ShouldStartWith($"{code}: ");
    }

    private void SignInAs(User user)
        => FactoryClient.SetFakeBearerToken(new Dictionary<string, object> { ["sub"] = user.Identifier });

    private static async Task<(Tenant Tenant, User User)> AddTenantWithUserAsync()
    {
        TestGraph.Start(out _)
            .AddTenant(out var tenant)
            .AddUser(out var user, u => u.Role = "User");
        await InsertAsync(tenant);
        await InsertAsync(user);
        return (tenant, user);
    }
}
