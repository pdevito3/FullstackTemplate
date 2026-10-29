namespace FullstackTemplate.FunctionalTests.FunctionalTests.Users;

using System.Net;
using System.Net.Http.Json;
using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Resources.Entrypoints;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using TestUtilities;

public class UserCheckOrderTests : TestBase
{
    [Test]
    public async Task body_that_does_not_bind_is_a_bad_request_for_a_caller_without_the_permission()
    {
        // Arrange
        await AddUserAsync("User");
        var dto = new FakeUserForUpdateDto().Generate();

        // Act
        var result = await FactoryClient.PutJsonRequestAsync(ApiRoutes.Users.Put(UnknownUserId()),
            new { dto.FirstName, dto.LastName, dto.Email, dto.Username });

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Errors.ShouldHaveSingleItem().Key.ShouldBe("version");
    }

    [Test]
    public async Task unknown_id_is_forbidden_for_a_caller_without_the_permission()
    {
        // Arrange
        await AddUserAsync("User");

        // Act
        var result = await FactoryClient.PutJsonRequestAsync(ApiRoutes.Users.Put(UnknownUserId()),
            new FakeUserForUpdateDto().Generate() with { Version = 1 });

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task unknown_permission_to_give_is_a_validation_error_before_the_not_found_of_an_unknown_id()
    {
        // Arrange
        await AddUserAsync("Admin");

        // Act
        var result = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Users.AddPermission(UnknownUserId()),
            new { permission = "NotAPermission" });

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Errors.ShouldHaveSingleItem().Key.ShouldBe("permission");
    }

    [Test]
    public async Task unknown_id_is_not_found_for_a_body_that_breaks_a_rule()
    {
        // Arrange
        await AddUserAsync("Admin");

        // Act
        var result = await FactoryClient.PutJsonRequestAsync(ApiRoutes.Users.Put(UnknownUserId()),
            new FakeUserForUpdateDto().Generate() with { FirstName = " ", Version = 1 });

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task stale_version_is_a_stale_version_for_a_body_that_breaks_a_rule()
    {
        // Arrange
        var admin = await AddUserAsync("Admin");
        var first = await FactoryClient.PutJsonRequestAsync(ApiRoutes.Users.Put(admin.Id),
            new FakeUserForUpdateDto().Generate() with { Version = admin.Version });
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Act
        var result = await FactoryClient.PutJsonRequestAsync(ApiRoutes.Users.Put(admin.Id),
            new FakeUserForUpdateDto().Generate() with { FirstName = " ", Version = admin.Version });

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Type.ShouldBe(DomainProblemDetails.StaleVersionType);
    }

    private async Task<User> AddUserAsync(string role)
    {
        TestGraph.Start(out _)
            .AddTenant(out var tenant)
            .AddUser(out var user, u => u.Role = role);
        await InsertAsync(tenant);
        await InsertAsync(user);
        FactoryClient.SignInAsAppUser(user.Identifier);
        return user;
    }

    private static string UnknownUserId() => EntityId.New(User.IdPrefix);
}
