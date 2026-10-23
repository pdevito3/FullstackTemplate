namespace FullstackTemplate.FunctionalTests.FunctionalTests.Users;

using System.Net;
using System.Net.Http.Json;
using FullstackTemplate.Server.Domain.Users.Dtos;
using FullstackTemplate.Server.Resources.Entrypoints;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using TestUtilities;

public class UserConcurrencyTests : TestBase
{
    [Test]
    public async Task put_with_a_stale_version_is_a_stale_version_problem_on_the_version()
    {
        // Arrange
        await StartGraph()
            .AddTenant(out _)
            .AddUser(out var user, u => u.Role = "Admin")
            .BuildAsync();
        FactoryClient.SignInAsAppUser(user.Identifier);
        var first = await FactoryClient.PutJsonRequestAsync(ApiRoutes.Users.Put(user.Id),
            new FakeUserForUpdateDto().Generate() with { Version = user.Version });
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Act
        var result = await FactoryClient.PutJsonRequestAsync(ApiRoutes.Users.Put(user.Id),
            new FakeUserForUpdateDto().Generate() with { Version = user.Version });

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        result.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Type.ShouldBe(DomainProblemDetails.StaleVersionType);
        problem.Title.ShouldBe("The record changed after you loaded it.");
        problem.Errors.ShouldHaveSingleItem().Key.ShouldBe("version");
        var saved = (await (await FactoryClient.GetRequestAsync(ApiRoutes.Users.GetRecord(user.Id)))
            .Content.ReadFromJsonAsync<UserDto>()).ShouldNotBeNull();
        saved.FirstName.ShouldBe((await first.Content.ReadFromJsonAsync<UserDto>())!.FirstName);
    }
}
