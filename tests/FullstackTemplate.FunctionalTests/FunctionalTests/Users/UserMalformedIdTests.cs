namespace FullstackTemplate.FunctionalTests.FunctionalTests.Users;

using System.Net;
using Shouldly;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.SharedTestHelpers.Fakes.Tenant;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using TestUtilities;

public class UserMalformedIdTests : TestBase
{
    private static readonly Dictionary<string, HttpStatusCode> SuccessCodes = new()
    {
        ["GET"] = HttpStatusCode.OK,
        ["PUT"] = HttpStatusCode.OK,
        ["DELETE"] = HttpStatusCode.NoContent,
        ["role"] = HttpStatusCode.OK,
        ["add-permission"] = HttpStatusCode.OK,
        ["remove-permission"] = HttpStatusCode.OK,
    };

    public static IEnumerable<(string, string)> Cases => MalformedIds.Cases([.. SuccessCodes.Keys]);

    public static IEnumerable<(string, HttpStatusCode)> Targets
        => SuccessCodes.Select(target => (target.Key, target.Value));

    [Test]
    [MethodDataSource(nameof(Cases))]
    public async Task malformed_user_id_returns_not_found_problem(string target, string kind)
    {
        // Arrange
        var id = MalformedIds.From(kind, await AddUserAsync(), Tenant.IdPrefix);
        SignInAsTrustedClient();

        // Act
        var result = await SendAsync(target, id);

        // Assert
        await MalformedIds.ShouldBeNotFoundAsync(result, "User was not found.");
    }

    [Test]
    [MethodDataSource(nameof(Targets))]
    public async Task correct_user_id_finds_the_record(string target, HttpStatusCode expected)
    {
        // Arrange
        var id = await AddUserAsync();
        SignInAsTrustedClient();

        // Act
        var result = await SendAsync(target, id);

        // Assert
        result.StatusCode.ShouldBe(expected);
    }

    private Task<HttpResponseMessage> SendAsync(string target, string id) => target switch
    {
        "GET" => FactoryClient.GetRequestAsync(ApiRoutes.Users.GetRecord(id)),
        "PUT" => FactoryClient.PutJsonRequestAsync(ApiRoutes.Users.Put(id), new FakeUserForUpdateDto().Generate()),
        "DELETE" => FactoryClient.DeleteRequestAsync(ApiRoutes.Users.Delete(id)),
        "role" => FactoryClient.PutJsonRequestAsync(ApiRoutes.Users.UpdateRole(id), new { role = "Admin" }),
        "add-permission" => FactoryClient.PostJsonRequestAsync(
            ApiRoutes.Users.AddPermission(id), new { permission = "do_something_special" }),
        "remove-permission" => FactoryClient.DeleteRequestAsync(
            ApiRoutes.Users.RemovePermission(id, "do_something_special")),
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, null),
    };

    private void SignInAsTrustedClient()
        => FactoryClient.SetFakeBearerToken(new Dictionary<string, object>
        {
            ["client_id"] = TestingWebApplicationFactory.TrustedMachineClientId
        });

    private static async Task<string> AddUserAsync()
    {
        var tenant = Tenant.Create(new FakeTenantForCreation().Generate());
        var user = User.Create(new FakeUserForCreation().RuleFor(u => u.TenantId, tenant.Id).Generate());
        await InsertAsync(tenant);
        await InsertAsync(user);
        return user.Id;
    }
}
