namespace FullstackTemplate.FunctionalTests.FunctionalTests.Agents;

using System.Net;
using System.Net.Http.Json;
using FullstackTemplate.Server.Domain.Agents;
using FullstackTemplate.Server.Domain.Agents.Dtos;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using TestUtilities;

public class AgentErrorResponseTests : TestBase
{
    [Test]
    public async Task invalid_permission_of_the_first_grant_is_an_error_under_its_item()
    {
        // Arrange
        TestGraph.Start(out _)
            .AddTenant(out var tenant)
            .AddUser(out var admin, u => u.Role = "Admin")
            .Under(tenant);
        await InsertAsync(tenant);
        await InsertAsync(admin);
        FactoryClient.SignInAsAppUser(admin.Identifier);

        // Act
        var result = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Agents.Create, new AgentForCreationDto
        {
            Name = "Clinic assistant",
            ClientId = $"clinic-assistant-{Guid.NewGuid():N}",
            Mode = AgentMode.DelegatedValue,
            Permissions = [Permission.Names.ReadUsers, "nope"],
        });

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        var error = problem.Errors.ShouldHaveSingleItem();
        error.Key.ShouldBe("permissions[1]");
        error.Value.ShouldBe(["Invalid permission: nope"]);
    }
}
