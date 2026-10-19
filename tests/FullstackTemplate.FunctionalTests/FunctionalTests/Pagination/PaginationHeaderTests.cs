namespace FullstackTemplate.FunctionalTests.FunctionalTests.Pagination;

using System.Net;
using System.Text.Json;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.SharedTestHelpers.Fakes.Tenant;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using Shouldly;
using TestUtilities;

public class PaginationHeaderTests : TestBase
{
    // The names and value types that parsePagination in frontend/src/lib/pagination.ts reads.
    // They are literals on purpose, so that a rename on the Server fails this test.
    private static readonly Dictionary<string, JsonValueKind> ExpectedProperties = new()
    {
        ["totalCount"] = JsonValueKind.Number,
        ["pageSize"] = JsonValueKind.Number,
        ["currentPageSize"] = JsonValueKind.Number,
        ["currentStartIndex"] = JsonValueKind.Number,
        ["currentEndIndex"] = JsonValueKind.Number,
        ["pageNumber"] = JsonValueKind.Number,
        ["totalPages"] = JsonValueKind.Number,
        ["hasPrevious"] = JsonValueKind.False,
        ["hasNext"] = JsonValueKind.True,
    };

    [Test]
    public async Task user_list_endpoint_sends_pagination_header_with_the_expected_shape()
    {
        // Arrange
        var tenant = Tenant.Create(new FakeTenantForCreation().Generate());
        await InsertAsync(tenant);
        var users = Enumerable.Range(0, 2)
            .Select(_ => User.Create(new FakeUserForCreation().RuleFor(u => u.TenantId, tenant.Id).Generate()))
            .ToArray();
        await InsertAsync(users);
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object> { ["sub"] = users[0].Identifier });

        // Act
        var result = await FactoryClient.GetRequestAsync($"{ApiRoutes.Users.GetList}?pageNumber=1&pageSize=1");

        // Assert
        ShouldHavePaginationHeaderForFirstOfTwoPages(result);
    }

    [Test]
    public async Task tenant_list_endpoint_sends_pagination_header_with_the_expected_shape()
    {
        // Arrange
        // A user sees only their own tenant, so a list of two tenants needs a caller with all-tenant access.
        // That caller sees the tenants of other tests too, so a filter on a unique name keeps them out.
        var marker = Guid.NewGuid().ToString("N");
        var tenants = Enumerable.Range(0, 2)
            .Select(i => Tenant.Create(new FakeTenantForCreation().RuleFor(t => t.Name, _ => $"Tenant {marker} {i}").Generate()))
            .ToArray();
        await InsertAsync(tenants);
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object>
        {
            ["client_id"] = TestingWebApplicationFactory.TrustedMachineClientId
        });
        var filters = Uri.EscapeDataString($"Name @= \"{marker}\"");

        // Act
        var result = await FactoryClient.GetRequestAsync($"{ApiRoutes.Tenants.GetList}?pageNumber=1&pageSize=1&filters={filters}");

        // Assert
        ShouldHavePaginationHeaderForFirstOfTwoPages(result);
    }

    private static void ShouldHavePaginationHeaderForFirstOfTwoPages(HttpResponseMessage result)
    {
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.Headers.TryGetValues("X-Pagination", out var values).ShouldBeTrue();
        using var header = JsonDocument.Parse(values.ShouldHaveSingleItem());
        var properties = header.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ValueKind);
        properties.ShouldBe(ExpectedProperties, ignoreOrder: true);
        header.RootElement.GetProperty("totalCount").GetInt32().ShouldBe(2);
        header.RootElement.GetProperty("pageSize").GetInt32().ShouldBe(1);
        header.RootElement.GetProperty("totalPages").GetInt32().ShouldBe(2);
    }
}
