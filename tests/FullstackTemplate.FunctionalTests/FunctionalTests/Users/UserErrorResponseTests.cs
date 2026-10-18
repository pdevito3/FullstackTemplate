namespace FullstackTemplate.FunctionalTests.FunctionalTests.Users;

using System.Net;
using System.Net.Http.Json;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.SharedTestHelpers.Fakes.Tenant;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using TestUtilities;

public class UserErrorResponseTests : TestBase
{
    [Fact]
    public async Task blank_email_returns_validation_problem()
    {
        // Arrange
        var tenant = Tenant.Create(new FakeTenantForCreation().Generate());
        var user = User.Create(new FakeUserForCreation().RuleFor(u => u.TenantId, tenant.Id).Generate());
        await InsertAsync(tenant);
        await InsertAsync(user);
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object> { ["sub"] = user.Identifier });
        var dto = new FakeUserForCreationDto()
            .RuleFor(x => x.TenantId, _ => tenant.Id)
            .RuleFor(x => x.Email, _ => "")
            .Generate();

        // Act
        var result = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Users.Create, dto);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Status.ShouldBe(StatusCodes.Status422UnprocessableEntity);
        problem.Detail.ShouldBe("Please provide a valid email.");
    }

    [Fact]
    public async Task unknown_identifier_returns_not_found_problem()
    {
        // Arrange
        FactoryClient.SetFakeBearerToken("user");

        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Users.GetByIdentifier($"unknown-{Guid.NewGuid()}"));

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = (await result.Content.ReadFromJsonAsync<ProblemDetails>()).ShouldNotBeNull();
        problem.Status.ShouldBe(StatusCodes.Status404NotFound);
    }
}
