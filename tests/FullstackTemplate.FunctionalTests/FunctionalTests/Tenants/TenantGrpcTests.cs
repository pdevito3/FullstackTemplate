namespace FullstackTemplate.FunctionalTests.FunctionalTests.Tenants;

using System.Net;
using FullstackTemplate.Server.Domain.Tenants.Grpc.V1;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using global::Google.Rpc;
using global::Grpc.Core;
using Shouldly;
using TestUtilities;
using DomainTenant = FullstackTemplate.Server.Domain.Tenants.Tenant;

/// <summary>
/// The gRPC entrypoint sends the same MediatR requests as HTTP, so it has the same tenant scope and the
/// same access rules. These tests check the transport: auth, the scope, and the status code of each domain error.
/// </summary>
public class TenantGrpcTests : TestBase
{
    [Test]
    public async Task user_can_get_their_own_tenant()
    {
        // Arrange
        var (tenant, user) = await AddTenantWithUserAsync();
        SignInAs(user);
        using var channel = CreateGrpcChannel();
        var client = new TenantsService.TenantsServiceClient(channel);

        // Act
        var result = await client.GetTenantAsync(new GetTenantRequest { Id = tenant.Id });

        // Assert
        result.Tenant.Id.ShouldBe(tenant.Id);
        result.Tenant.Name.ShouldBe(tenant.Name);
    }

    [Test]
    public async Task user_cannot_get_another_tenant()
    {
        // Arrange
        var (_, user) = await AddTenantWithUserAsync();
        var (otherTenant, _) = await AddTenantWithUserAsync();
        SignInAs(user);
        using var channel = CreateGrpcChannel();
        var client = new TenantsService.TenantsServiceClient(channel);

        // Act
        var exception = await Should.ThrowAsync<RpcException>(
            async () => await client.GetTenantAsync(new GetTenantRequest { Id = otherTenant.Id }));

        // Assert
        exception.StatusCode.ShouldBe(StatusCode.NotFound);
    }

    [Test]
    public async Task user_sees_only_their_own_tenant_in_the_list()
    {
        // Arrange
        var (tenant, user) = await AddTenantWithUserAsync();
        await AddTenantWithUserAsync();
        SignInAs(user);
        using var channel = CreateGrpcChannel();
        var client = new TenantsService.TenantsServiceClient(channel);

        // Act
        var result = await client.ListTenantsAsync(new ListTenantsRequest { PageSize = 500 });

        // Assert
        result.Tenants.ShouldHaveSingleItem().Id.ShouldBe(tenant.Id);
        result.Pagination.TotalCount.ShouldBe(1);
        result.Pagination.PageNumber.ShouldBe(1);
        result.Pagination.PageSize.ShouldBe(500);
    }

    [Test]
    public async Task list_with_no_page_fields_gives_the_first_page_with_the_default_size()
    {
        // Arrange
        var (_, user) = await AddTenantWithUserAsync();
        SignInAs(user);
        using var channel = CreateGrpcChannel();
        var client = new TenantsService.TenantsServiceClient(channel);

        // Act
        var result = await client.ListTenantsAsync(new ListTenantsRequest());

        // Assert
        result.Pagination.PageNumber.ShouldBe(1);
        result.Pagination.PageSize.ShouldBe(FullstackTemplate.Server.Resources.BasePaginationParameters.DefaultPageSizeValue);
    }

    [Test]
    [Arguments(-1, 0)]
    [Arguments(0, -1)]
    public async Task page_out_of_range_is_an_invalid_argument(int pageNumber, int pageSize)
    {
        // Arrange
        var (_, user) = await AddTenantWithUserAsync();
        SignInAs(user);
        using var channel = CreateGrpcChannel();
        var client = new TenantsService.TenantsServiceClient(channel);

        // Act
        var exception = await Should.ThrowAsync<RpcException>(async () =>
            await client.ListTenantsAsync(new ListTenantsRequest { PageNumber = pageNumber, PageSize = pageSize }));

        // Assert
        exception.StatusCode.ShouldBe(StatusCode.InvalidArgument);
    }

    [Test]
    [Arguments("Name @= ")]
    [Arguments("version > 5")]
    public async Task bad_filter_is_an_invalid_argument(string filters)
    {
        // Arrange
        var (_, user) = await AddTenantWithUserAsync();
        SignInAs(user);
        using var channel = CreateGrpcChannel();
        var client = new TenantsService.TenantsServiceClient(channel);

        // Act
        var exception = await Should.ThrowAsync<RpcException>(async () =>
            await client.ListTenantsAsync(new ListTenantsRequest { Filters = filters }));

        // Assert
        exception.StatusCode.ShouldBe(StatusCode.InvalidArgument);
    }

    [Test]
    public async Task sort_on_a_field_that_the_list_does_not_have_names_it_to_the_caller_and_not_to_the_logs()
    {
        // Arrange
        var (_, user) = await AddTenantWithUserAsync();
        SignInAs(user);
        using var channel = CreateGrpcChannel();
        var client = new TenantsService.TenantsServiceClient(channel);
        var email = $"jane{Guid.NewGuid():N}@example.com";

        // Act
        var exception = await Should.ThrowAsync<RpcException>(async () =>
            await client.ListTenantsAsync(new ListTenantsRequest { SortOrder = email }));

        // Assert
        exception.StatusCode.ShouldBe(StatusCode.InvalidArgument);
        exception.Status.Detail.ShouldBe(
            $"The sort order names the field {email}, which this list does not have. Use one of these fields: id, name.");
        TestingWebApplicationFactory.Instance.CapturedLogs.Holding(email).ShouldBeEmpty();
    }

    [Test]
    public async Task user_cannot_add_a_tenant()
    {
        // Arrange
        var (_, user) = await AddTenantWithUserAsync();
        SignInAs(user);
        using var channel = CreateGrpcChannel();
        var client = new TenantsService.TenantsServiceClient(channel);

        // Act
        var exception = await Should.ThrowAsync<RpcException>(async () =>
            await client.AddTenantAsync(new AddTenantRequest { Name = $"Tenant {Guid.NewGuid()}" }));

        // Assert
        exception.StatusCode.ShouldBe(StatusCode.PermissionDenied);
    }

    [Test]
    public async Task trusted_machine_client_can_add_a_tenant_once()
    {
        // Arrange
        SignInAsTrustedMachineClient();
        using var channel = CreateGrpcChannel();
        var client = new TenantsService.TenantsServiceClient(channel);
        var request = new AddTenantRequest { Name = $"Tenant {Guid.NewGuid()}" };

        // Act
        var added = await client.AddTenantAsync(request);
        var duplicate = await Should.ThrowAsync<RpcException>(async () => await client.AddTenantAsync(request));

        // Assert
        added.Tenant.Name.ShouldBe(request.Name);
        (await FindAsync<DomainTenant>(added.Tenant.Id)).ShouldNotBeNull();
        duplicate.StatusCode.ShouldBe(StatusCode.AlreadyExists);
        duplicate.Status.Detail.ShouldBe("A tenant with this name already exists.");
    }

    [Test]
    public async Task empty_tenant_name_is_an_invalid_argument_with_a_field_violation()
    {
        // Arrange
        SignInAsTrustedMachineClient();
        using var channel = CreateGrpcChannel();
        var client = new TenantsService.TenantsServiceClient(channel);

        // Act
        var exception = await Should.ThrowAsync<RpcException>(async () =>
            await client.AddTenantAsync(new AddTenantRequest { Name = "" }));

        // Assert
        exception.StatusCode.ShouldBe(StatusCode.InvalidArgument);
        var violation = exception.GetRpcStatus().ShouldNotBeNull().GetDetail<BadRequest>().ShouldNotBeNull()
            .FieldViolations.ShouldHaveSingleItem();
        violation.Field.ShouldBe("name");
        violation.Description.ShouldBe("Please provide a tenant name.");
    }

    [Test]
    public async Task nul_in_a_tenant_name_is_an_invalid_argument_with_a_field_violation()
    {
        // Arrange
        SignInAsTrustedMachineClient();
        using var channel = CreateGrpcChannel();
        var client = new TenantsService.TenantsServiceClient(channel);

        // Act
        var exception = await Should.ThrowAsync<RpcException>(async () =>
            await client.AddTenantAsync(new AddTenantRequest { Name = $"Tenant {Guid.NewGuid()}\0" }));

        // Assert
        exception.StatusCode.ShouldBe(StatusCode.InvalidArgument);
        var violation = exception.GetRpcStatus().ShouldNotBeNull().GetDetail<BadRequest>().ShouldNotBeNull()
            .FieldViolations.ShouldHaveSingleItem();
        violation.Field.ShouldBe("name");
        violation.Description.ShouldBe("A tenant name has a character that is not allowed.");
    }

    [Test]
    public async Task call_with_no_token_is_unauthenticated()
    {
        // Arrange
        using var channel = CreateGrpcChannel();
        var client = new TenantsService.TenantsServiceClient(channel);

        // Act
        var exception = await Should.ThrowAsync<RpcException>(async () =>
            await client.ListTenantsAsync(new ListTenantsRequest()));

        // Assert
        exception.StatusCode.ShouldBe(StatusCode.Unauthenticated);
    }

    private void SignInAs(User user)
        => FactoryClient.SignInAsAppUser(user.Identifier);

    private void SignInAsTrustedMachineClient()
        => FactoryClient.SetFakeBearerToken(new Dictionary<string, object>
        {
            ["client_id"] = TestingWebApplicationFactory.TrustedMachineClientId
        });

    private static async Task<(DomainTenant Tenant, User User)> AddTenantWithUserAsync()
    {
        TestGraph.Start(out _)
            .AddTenant(out var tenant)
            .AddUser(out var user, u => u.Role = "User");
        await InsertAsync(tenant);
        await InsertAsync(user);
        return (tenant, user);
    }
}
