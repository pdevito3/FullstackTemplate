namespace FullstackTemplate.FunctionalTests.FunctionalTests.Tenants;

using System.Net;
using System.Text;
using Shouldly;
using TestUtilities;
using WebMotions.Fake.Authentication.JwtBearer;

public class AddTenantTests : TestBase
{
    [Fact]
    public async Task text_json_body_is_accepted()
    {
        // Arrange
        FactoryClient.SetFakeBearerToken("user");
        using var content = new StringContent($"{{ \"name\": \"Tenant {Guid.NewGuid()}\" }}", Encoding.UTF8, "text/json");

        // Act
        var result = await FactoryClient.PostAsync(ApiRoutes.Tenants.Create, content);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.Created);
    }
}
