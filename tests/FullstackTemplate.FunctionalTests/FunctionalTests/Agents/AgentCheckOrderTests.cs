namespace FullstackTemplate.FunctionalTests.FunctionalTests.Agents;

using System.Net;
using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.Agents;
using FullstackTemplate.Server.Domain.Agents.Dtos;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Shouldly;
using TestUtilities;

public class AgentCheckOrderTests : TestBase
{
    [Test]
    public async Task grant_of_a_permission_the_caller_lacks_is_forbidden_for_an_unknown_agent()
    {
        // Arrange
        TestGraph.Start(out _)
            .AddTenant(out var tenant)
            .AddUser(out var member, u => u.Role = "User")
            .Under(tenant);
        member.AddPermissions([Permission.ManageAgents(), Permission.ReadAgents()]);
        await InsertAsync(tenant);
        await InsertAsync(member);
        FactoryClient.SignInAsAppUser(member.Identifier);

        // Act
        var result = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Agents.AddPermission(EntityId.New(Agent.IdPrefix)),
            new AgentPermissionDto { Permission = Permission.Names.ManageTenant });

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task first_grant_of_a_permission_the_caller_lacks_is_forbidden_before_the_rules_on_the_new_values()
    {
        // Arrange
        TestGraph.Start(out _)
            .AddTenant(out var tenant)
            .AddUser(out var member, u => u.Role = "User")
            .Under(tenant);
        member.AddPermissions([Permission.ManageAgents(), Permission.ReadAgents()]);
        await InsertAsync(tenant);
        await InsertAsync(member);
        FactoryClient.SignInAsAppUser(member.Identifier);

        // Act
        var result = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Agents.Create, new AgentForCreationDto
        {
            Name = "agent",
            ClientId = " client-with-a-leading-space",
            Mode = "delegated",
            Permissions = [Permission.Names.ManageTenant]
        });

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
