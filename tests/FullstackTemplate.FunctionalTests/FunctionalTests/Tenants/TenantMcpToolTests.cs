namespace FullstackTemplate.FunctionalTests.FunctionalTests.Tenants;

using System.Net;
using System.Text;
using System.Text.Json;
using FullstackTemplate.Server.Domain.Agents;
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
using TestUtilities;

/// <summary>
/// The MCP entrypoint sends the same MediatR requests as HTTP, so it has the same tenant scope and the
/// same access rules. These tests check the transport: auth, the scope, and the tool error of each domain error.
/// </summary>
public class TenantMcpToolTests : TestBase
{
    private const string UnregisteredClientId = "unregistered-agent";

    public static IEnumerable<Func<(string Caller, string[] Tools)>> ToolsOfEachCaller()
    {
        yield return () => ("member", ["get_tenant", "list_tenants"]);
        yield return () => ("admin", ["get_tenant", "list_tenants", "update_tenant"]);
        yield return () => ("manage-all client", ["add_tenant", "get_tenant", "list_tenants", "update_tenant"]);
        yield return () => ("read-only client", ["get_tenant", "list_tenants"]);
        yield return () => ("unregistered client with a user sub", []);
        yield return () => ("admin through a delegated agent with no grant", ["get_tenant", "list_tenants"]);
        yield return () => ("admin through a delegated agent granted manage_tenant",
            ["get_tenant", "list_tenants", "update_tenant"]);
        yield return () => ("machine agent granted manage_tenant", ["get_tenant", "list_tenants", "update_tenant"]);
    }

    [Test]
    [MethodDataSource(nameof(ToolsOfEachCaller))]
    public async Task tools_list_shows_only_the_tools_that_the_caller_can_use(string caller, string[] expected)
    {
        // Arrange
        await SignInAsync(caller);
        await using var client = await CreateMcpClientAsync();

        // Act
        var tools = await client.ListToolsAsync();

        // Assert
        tools.Select(t => t.Name).ShouldBe(expected, ignoreOrder: true);
    }

    [Test]
    public async Task unregistered_client_with_a_user_sub_cannot_read_the_tenant_of_the_user()
    {
        // Arrange
        var (tenant, admin) = await AddTenantWithUserAsync("Admin");
        SignInThroughUnregisteredClient(admin);
        await using var client = await CreateMcpClientAsync();

        // Act
        var result = await client.CallToolAsync("get_tenant", new Dictionary<string, object?> { ["id"] = tenant.Id });

        // Assert
        ShouldBeToolError(result, "permission_denied");
        result.StructuredContent.ShouldNotBeNull().GetProperty("detail").GetString()
            .ShouldBe("This client is not a registered agent of your tenant. A tenant admin can register it.");
    }

    [Test]
    public async Task admin_through_a_delegated_agent_cannot_rename_beyond_the_grant()
    {
        // Arrange
        var (tenant, admin, agent) = await AddTenantWithAgentAsync("delegated");
        SignInThroughAgent(admin, agent);
        await using var client = await CreateMcpClientAsync();

        // Act
        var result = await client.CallToolAsync("update_tenant",
            new Dictionary<string, object?> { ["id"] = tenant.Id, ["name"] = $"Renamed {Guid.NewGuid()}" });

        // Assert
        ShouldBeToolError(result, "permission_denied");
        result.StructuredContent.ShouldNotBeNull().GetProperty("detail").GetString().ShouldBe(
            $"The agent {agent.ClientId} is not granted the manage_tenant permission. A tenant admin can grant it.");
        (await FindAsync<Tenant>(tenant.Id)).ShouldNotBeNull().Name.ShouldBe(tenant.Name);
    }

    [Test]
    public async Task machine_agent_with_a_service_account_sub_lists_only_its_tenant()
    {
        // Arrange
        var (tenant, _, agent) = await AddTenantWithAgentAsync("machine");
        await AddTenantWithUserAsync();
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object>
        {
            ["sub"] = $"service-account-{agent.ClientId}",
            ["azp"] = agent.ClientId,
        });
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

    [Test]
    public async Task user_without_manage_tenant_cannot_rename_their_tenant()
    {
        // Arrange
        var (tenant, user) = await AddTenantWithUserAsync();
        SignInAs(user);
        await using var client = await CreateMcpClientAsync();

        // Act
        var result = await client.CallToolAsync("update_tenant",
            new Dictionary<string, object?>
            {
                ["id"] = tenant.Id,
                ["name"] = $"Renamed {Guid.NewGuid()}",
                ["version"] = await VersionOfAsync(tenant.Id)
            });

        // Assert
        ShouldBeToolError(result, "permission_denied");
        result.StructuredContent.ShouldNotBeNull().GetProperty("detail").GetString().ShouldNotBeNull()
            .ShouldContain("manage_tenant");
        (await FindAsync<Tenant>(tenant.Id)).ShouldNotBeNull().Name.ShouldBe(tenant.Name);
    }

    [Test]
    public async Task admin_can_rename_their_tenant()
    {
        // Arrange
        var (tenant, admin) = await AddTenantWithUserAsync("Admin");
        SignInAs(admin);
        await using var client = await CreateMcpClientAsync();
        var name = $"Renamed {Guid.NewGuid()}";

        // Act
        var result = await client.CallToolAsync("update_tenant",
            new Dictionary<string, object?>
            {
                ["id"] = tenant.Id,
                ["name"] = name,
                ["version"] = await VersionOfAsync(tenant.Id)
            });

        // Assert
        result.IsError.ShouldNotBe(true);
        result.StructuredContent.ShouldNotBeNull().GetProperty("name").GetString().ShouldBe(name);
        (await FindAsync<Tenant>(tenant.Id)).ShouldNotBeNull().Name.ShouldBe(name);
    }

    [Test]
    public async Task rename_with_a_stale_version_is_the_aborted_tool_error()
    {
        // Arrange
        var (tenant, admin) = await AddTenantWithUserAsync("Admin");
        SignInAs(admin);
        await using var client = await CreateMcpClientAsync();
        var loaded = await VersionOfAsync(tenant.Id);
        var name = $"Renamed {Guid.NewGuid()}";
        var first = await client.CallToolAsync("update_tenant",
            new Dictionary<string, object?> { ["id"] = tenant.Id, ["name"] = name, ["version"] = loaded });
        first.IsError.ShouldNotBe(true);

        // Act
        var result = await client.CallToolAsync("update_tenant",
            new Dictionary<string, object?>
            {
                ["id"] = tenant.Id,
                ["name"] = $"Renamed {Guid.NewGuid()}",
                ["version"] = loaded
            });

        // Assert
        ShouldBeToolError(result, "aborted");
        (await FindAsync<Tenant>(tenant.Id)).ShouldNotBeNull().Name.ShouldBe(name);
    }

    [Test]
    public async Task read_only_machine_client_cannot_rename_a_tenant()
    {
        // Arrange
        var (tenant, _) = await AddTenantWithUserAsync();
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object>
        {
            ["client_id"] = TestingWebApplicationFactory.ReadOnlyMachineClientId
        });
        await using var client = await CreateMcpClientAsync();

        // Act
        var result = await client.CallToolAsync("update_tenant",
            new Dictionary<string, object?>
            {
                ["id"] = tenant.Id,
                ["name"] = $"Renamed {Guid.NewGuid()}",
                ["version"] = await VersionOfAsync(tenant.Id)
            });

        // Assert
        ShouldBeToolError(result, "permission_denied");
        (await FindAsync<Tenant>(tenant.Id)).ShouldNotBeNull().Name.ShouldBe(tenant.Name);
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

    private static async Task<uint> VersionOfAsync(string tenantId)
        => (await FindAsync<Tenant>(tenantId)).ShouldNotBeNull().Version;

    private void SignInAs(User user)
        => FactoryClient.SignInAsAppUser(user.Identifier);

    private void SignInThroughAgent(User user, Agent agent)
        => FactoryClient.SetFakeBearerToken(new Dictionary<string, object>
        {
            ["sub"] = user.Identifier,
            ["azp"] = agent.ClientId,
        });

    private void SignInAsMachineAgent(Agent agent)
        => FactoryClient.SetFakeBearerToken(new Dictionary<string, object> { ["client_id"] = agent.ClientId });

    private void SignInThroughUnregisteredClient(User user)
        => FactoryClient.SetFakeBearerToken(new Dictionary<string, object>
        {
            ["sub"] = user.Identifier,
            ["azp"] = UnregisteredClientId,
        });

    private async Task SignInAsync(string caller)
    {
        switch (caller)
        {
            case "member":
                SignInAs((await AddTenantWithUserAsync()).User);
                break;
            case "admin":
                SignInAs((await AddTenantWithUserAsync("Admin")).User);
                break;
            case "manage-all client":
                FactoryClient.SetFakeBearerToken(new Dictionary<string, object>
                {
                    ["client_id"] = TestingWebApplicationFactory.TrustedMachineClientId
                });
                break;
            case "read-only client":
                FactoryClient.SetFakeBearerToken(new Dictionary<string, object>
                {
                    ["client_id"] = TestingWebApplicationFactory.ReadOnlyMachineClientId
                });
                break;
            case "unregistered client with a user sub":
                SignInThroughUnregisteredClient((await AddTenantWithUserAsync("Admin")).User);
                break;
            case "admin through a delegated agent with no grant":
            {
                var (_, admin, agent) = await AddTenantWithAgentAsync("delegated");
                SignInThroughAgent(admin, agent);
                break;
            }
            case "admin through a delegated agent granted manage_tenant":
            {
                var (_, admin, agent) = await AddTenantWithAgentAsync("delegated", Permission.Names.ManageTenant);
                SignInThroughAgent(admin, agent);
                break;
            }
            case "machine agent granted manage_tenant":
                SignInAsMachineAgent((await AddTenantWithAgentAsync("machine", Permission.Names.ManageTenant)).Agent);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(caller), caller, "No sign-in for this caller.");
        }
    }

    private static async Task<(Tenant Tenant, User Admin, Agent Agent)> AddTenantWithAgentAsync(
        string mode, params string[] grant)
    {
        TestGraph.Start(out _)
            .AddTenant(out var tenant)
            .AddUser(out var admin, u => u.Role = "Admin")
            .Under(tenant)
            .AddAgent(out var agent, a =>
            {
                a.Mode = mode;
                a.Permissions = grant;
            });
        await InsertAsync(tenant);
        await InsertAsync(admin);
        await InsertAsync(agent);
        return (tenant, admin, agent);
    }

    private static async Task<(Tenant Tenant, User User)> AddTenantWithUserAsync(string role = "User")
    {
        TestGraph.Start(out _)
            .AddTenant(out var tenant)
            .AddUser(out var user, u => u.Role = role);
        await InsertAsync(tenant);
        await InsertAsync(user);
        return (tenant, user);
    }
}
