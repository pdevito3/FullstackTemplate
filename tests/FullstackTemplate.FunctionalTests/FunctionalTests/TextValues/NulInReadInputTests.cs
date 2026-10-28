namespace FullstackTemplate.FunctionalTests.FunctionalTests.TextValues;

using System.Net;
using System.Net.Http.Json;
using FullstackTemplate.Server.Domain.Tenants.Grpc.V1;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using global::Google.Rpc;
using global::Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using TestUtilities;

/// <summary>
/// Postgres text cannot hold U+0000, so a read input with a NUL must not get to a query. A list value gives 400 on its
/// parameter, and an ID gives not found, the same as each other malformed ID, on each transport. An HTTP path cannot
/// carry a NUL, because the URL decoder of ASP.NET Core refuses it before the Server.
/// </summary>
public class NulInReadInputTests : TestBase
{
    private const string Nul = "a\0b";
    private const string NulId = "ten_a\0b";

    public static IEnumerable<(string, string)> ListQueryValues =>
    [
        (ApiRoutes.Users.GetList, "filters"),
        (ApiRoutes.Users.GetList, "sortOrder"),
        (ApiRoutes.Tenants.GetList, "filters"),
        (ApiRoutes.Tenants.GetList, "sortOrder"),
        (ApiRoutes.Agents.GetList, "filters"),
        (ApiRoutes.Agents.GetList, "sortOrder"),
        ($"{ApiRoutes.Base}/audit-entries", "entityId"),
        ($"{ApiRoutes.Base}/audit-entries", "cursor"),
    ];

    [Test]
    [MethodDataSource(nameof(ListQueryValues))]
    public async Task a_nul_in_a_list_query_value_returns_a_bad_request_problem_for_the_parameter(string route, string parameter)
    {
        // Arrange
        await SignInAsAdminAsync();
        var value = parameter == "filters" ? $"Id == \"{Nul}\"" : Nul;

        // Act
        var result = await FactoryClient.GetRequestAsync($"{route}?{parameter}={Uri.EscapeDataString(value)}");

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Errors.ShouldHaveSingleItem().Key.ShouldBe(parameter);
    }

    [Test]
    public async Task a_nul_in_the_tenant_id_of_a_grpc_get_is_not_found()
    {
        // Arrange
        await SignInAsAdminAsync();
        using var channel = CreateGrpcChannel();
        var client = new TenantsService.TenantsServiceClient(channel);

        // Act
        var exception = await Should.ThrowAsync<RpcException>(
            async () => await client.GetTenantAsync(new GetTenantRequest { Id = NulId }));

        // Assert
        exception.StatusCode.ShouldBe(StatusCode.NotFound);
    }

    [Test]
    [Arguments("filters")]
    [Arguments("sortOrder")]
    public async Task a_nul_in_a_list_value_over_grpc_is_an_invalid_argument_on_the_field(string field)
    {
        // Arrange
        await SignInAsAdminAsync();
        using var channel = CreateGrpcChannel();
        var client = new TenantsService.TenantsServiceClient(channel);
        var request = field == "filters"
            ? new ListTenantsRequest { Filters = $"Name == \"{Nul}\"" }
            : new ListTenantsRequest { SortOrder = Nul };

        // Act
        var exception = await Should.ThrowAsync<RpcException>(async () => await client.ListTenantsAsync(request));

        // Assert
        exception.StatusCode.ShouldBe(StatusCode.InvalidArgument);
        exception.GetRpcStatus().ShouldNotBeNull().GetDetail<BadRequest>().ShouldNotBeNull()
            .FieldViolations.ShouldHaveSingleItem().Field.ShouldBe(field);
    }

    // example:start
    [Test]
    [Arguments("get_tenant")]
    [Arguments("update_tenant")]
    public async Task a_nul_in_the_id_of_an_mcp_tool_is_not_found(string tool)
    {
        // Arrange
        await SignInAsAdminAsync();
        await using var client = await CreateMcpClientAsync();
        var arguments = new Dictionary<string, object?> { ["id"] = NulId };
        if (tool == "update_tenant")
        {
            arguments["name"] = $"Renamed {Guid.NewGuid()}";
            arguments["version"] = 1;
        }

        // Act
        var result = await client.CallToolAsync(tool, arguments);

        // Assert
        result.ShouldBeToolError("not_found");
    }

    [Test]
    [Arguments("filters")]
    [Arguments("sortOrder")]
    public async Task a_nul_in_a_list_value_over_mcp_is_an_invalid_argument_on_the_field(string field)
    {
        // Arrange
        await SignInAsAdminAsync();
        await using var client = await CreateMcpClientAsync();
        var value = field == "filters" ? $"Name == \"{Nul}\"" : Nul;

        // Act
        var result = await client.CallToolAsync("list_tenants", new Dictionary<string, object?> { [field] = value });

        // Assert
        result.ShouldBeToolError("invalid_argument");
        result.StructuredContent.ShouldNotBeNull().GetProperty("errors").EnumerateObject()
            .ShouldHaveSingleItem().Name.ShouldBe(field);
    }
    // example:end

    private async Task SignInAsAdminAsync()
    {
        TestGraph.Start(out _).AddTenant(out var tenant).AddUser(out var admin, u => u.Role = "Admin");
        await InsertAsync(tenant);
        await InsertAsync(admin);
        FactoryClient.SignInAsAppUser(admin.Identifier);
    }
}
