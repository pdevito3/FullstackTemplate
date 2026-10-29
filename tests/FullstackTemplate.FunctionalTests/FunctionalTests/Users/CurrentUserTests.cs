namespace FullstackTemplate.FunctionalTests.FunctionalTests.Users;

using System.Net;
using System.Net.Http.Json;
using FullstackTemplate.Server.Domain.Users.Dtos;
using Shouldly;
using TestUtilities;

public class CurrentUserTests : TestBase
{
    [Test]
    public async Task a_caller_with_no_token_is_unauthorized()
    {
        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Users.GetCurrent);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task a_signed_in_user_with_no_user_record_gets_the_subject_and_the_name_of_the_token()
    {
        // Arrange
        var subject = $"user-{Guid.NewGuid()}";
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object>
        {
            ["sub"] = subject,
            ["name"] = "Ada Lovelace",
            ["azp"] = TestingWebApplicationFactory.FirstPartyClientId,
        });

        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Users.GetCurrent);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var user = (await result.Content.ReadFromJsonAsync<CurrentUserDto>()).ShouldNotBeNull();
        user.Identifier.ShouldBe(subject);
        user.Name.ShouldBe("Ada Lovelace");
    }

    [Test]
    public async Task a_machine_client_with_no_subject_is_forbidden()
    {
        // Arrange
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object>
        {
            ["client_id"] = TestingWebApplicationFactory.TrustedMachineClientId
        });

        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Users.GetCurrent);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
