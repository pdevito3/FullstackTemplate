namespace FullstackTemplate.FunctionalTests.FunctionalTests.Tenants;

using System.Net;
using Bogus;
using Shouldly;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.SharedTestHelpers.Fakes.Tenant;
using TestUtilities;

public class TenantMalformedIdTests : TestBase
{
    private static readonly Dictionary<string, HttpStatusCode> SuccessCodes = new()
    {
        ["GET"] = HttpStatusCode.OK,
        ["PUT"] = HttpStatusCode.OK,
        ["DELETE"] = HttpStatusCode.NoContent,
    };

    public static IEnumerable<(string, string)> Cases => MalformedIds.Cases([.. SuccessCodes.Keys]);

    public static IEnumerable<(string, HttpStatusCode)> Targets
        => SuccessCodes.Select(target => (target.Key, target.Value));

    [Test]
    [MethodDataSource(nameof(Cases))]
    public async Task malformed_tenant_id_returns_not_found_problem(string method, string kind)
    {
        // Arrange
        var id = MalformedIds.From(kind, await AddTenantAsync(), User.IdPrefix);
        SignInAsTrustedClient();

        // Act
        var result = await SendAsync(method, id);

        // Assert
        await MalformedIds.ShouldBeNotFoundAsync(result, "Tenant was not found.");
    }

    [Test]
    [MethodDataSource(nameof(Targets))]
    public async Task correct_tenant_id_finds_the_record(string method, HttpStatusCode expected)
    {
        // Arrange
        var id = await AddTenantAsync();
        SignInAsTrustedClient();

        // Act
        var result = await SendAsync(method, id);

        // Assert
        result.StatusCode.ShouldBe(expected);
    }

    private Task<HttpResponseMessage> SendAsync(string method, string id) => method switch
    {
        "GET" => FactoryClient.GetRequestAsync(ApiRoutes.Tenants.GetRecord(id)),
        "PUT" => FactoryClient.PutJsonRequestAsync(ApiRoutes.Tenants.Put(id), new { name = TenantRules.Name(new Faker()) }),
        "DELETE" => FactoryClient.DeleteRequestAsync(ApiRoutes.Tenants.Delete(id)),
        _ => throw new ArgumentOutOfRangeException(nameof(method), method, null),
    };

    private void SignInAsTrustedClient()
        => FactoryClient.SetFakeBearerToken(new Dictionary<string, object>
        {
            ["client_id"] = TestingWebApplicationFactory.TrustedMachineClientId
        });

    private static async Task<string> AddTenantAsync()
    {
        var tenant = Tenant.Create(new FakeTenantForCreation().Generate());
        await InsertAsync(tenant);
        return tenant.Id;
    }
}
