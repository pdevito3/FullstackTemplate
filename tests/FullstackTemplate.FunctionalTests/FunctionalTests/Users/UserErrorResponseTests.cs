namespace FullstackTemplate.FunctionalTests.FunctionalTests.Users;

using System.Net;
using System.Net.Http.Json;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using TestUtilities;

public class UserErrorResponseTests : TestBase
{
    [Fact]
    public async Task blank_email_on_create_returns_validation_problem()
    {
        // Arrange
        TestGraph.Start(out _)
            .AddTenant(out var tenant)
            .AddUser(out var user, u => u.Role = "Admin");
        await InsertAsync(tenant);
        await InsertAsync(user);
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object> { ["sub"] = user.Identifier });
        var dto = new FakeUserForCreationDto()
            .RuleFor(x => x.Email, _ => "")
            .Generate();

        // Act
        var result = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Users.Create, dto);

        // Assert
        await ShouldBeBlankEmailProblem(result);
    }

    [Fact]
    public async Task blank_email_on_update_returns_validation_problem()
    {
        // Arrange
        TestGraph.Start(out _).AddTenant(out var tenant).AddUser(out var user);
        await InsertAsync(tenant);
        await InsertAsync(user);
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object> { ["sub"] = user.Identifier });
        var dto = new FakeUserForUpdateDto()
            .RuleFor(x => x.Email, _ => "")
            .Generate();

        // Act
        var result = await FactoryClient.PutJsonRequestAsync(ApiRoutes.Users.Put(user.Id), dto);

        // Assert
        await ShouldBeBlankEmailProblem(result);
    }

    private static async Task ShouldBeBlankEmailProblem(HttpResponseMessage result)
    {
        result.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Status.ShouldBe(StatusCodes.Status422UnprocessableEntity);
        problem.Detail.ShouldBe("Please provide a valid email.");
    }
}
