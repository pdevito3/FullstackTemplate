namespace FullstackTemplate.FunctionalTests.FunctionalTests.Users;

using System.Net;
using System.Net.Http.Json;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.Dtos;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using TestUtilities;

/// <summary>
/// A user with the read_users permission can read the other users of the tenant. The permission row decides, not
/// the role. A user reads their own record with no permission.
/// </summary>
public class UserReadAccessTests : TestBase
{
    private const string ReadUsersDenial = "You need the read_users permission to do this.";

    [Test]
    public async Task member_without_read_users_cannot_list_the_users()
    {
        // Arrange
        var (member, _) = await AddTwoUsersAsync("User", "User");
        SignInAs(member);

        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Users.GetList);

        // Assert
        await ShouldBeForbiddenAsync(result, ReadUsersDenial);
    }

    [Test]
    public async Task member_without_read_users_cannot_get_another_user()
    {
        // Arrange
        var (member, other) = await AddTwoUsersAsync("User", "User");
        SignInAs(member);

        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Users.GetRecord(other.Id));

        // Assert
        await ShouldBeForbiddenAsync(result, ReadUsersDenial);
    }

    [Test]
    public async Task member_without_read_users_cannot_get_another_user_by_identifier()
    {
        // Arrange
        var (member, other) = await AddTwoUsersAsync("User", "User");
        SignInAs(member);

        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Users.GetByIdentifier(other.Identifier));

        // Assert
        await ShouldBeForbiddenAsync(result, ReadUsersDenial);
    }

    // A 404 would tell the caller which identifiers exist
    [Test]
    public async Task member_without_read_users_is_forbidden_for_an_unknown_identifier()
    {
        // Arrange
        var (member, _) = await AddTwoUsersAsync("User", "User");
        SignInAs(member);

        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Users.GetByIdentifier($"unknown-{Guid.NewGuid()}"));

        // Assert
        await ShouldBeForbiddenAsync(result, ReadUsersDenial);
    }

    [Test]
    public async Task member_without_read_users_can_get_their_own_record()
    {
        // Arrange
        var (member, _) = await AddTwoUsersAsync("User", "User");
        SignInAs(member);

        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Users.GetRecord(member.Id));

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var user = (await result.Content.ReadFromJsonAsync<UserDto>()).ShouldNotBeNull();
        user.Id.ShouldBe(member.Id);
        user.Permissions.ShouldBeEmpty();
    }

    [Test]
    public async Task member_without_read_users_can_get_their_own_record_by_identifier()
    {
        // Arrange
        var (member, _) = await AddTwoUsersAsync("User", "User");
        SignInAs(member);

        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Users.GetByIdentifier(member.Identifier));

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var user = (await result.Content.ReadFromJsonAsync<UserDto>()).ShouldNotBeNull();
        user.Id.ShouldBe(member.Id);
        user.Permissions.ShouldBeEmpty();
    }

    [Test]
    public async Task user_role_member_granted_read_users_can_list_the_users()
    {
        // Arrange
        var (member, other) = await AddTwoUsersAsync("User", "User",
            first => first.AddPermission(Permission.ReadUsers()));
        SignInAs(member);

        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Users.GetList);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var users = (await result.Content.ReadFromJsonAsync<List<UserDto>>()).ShouldNotBeNull();
        users.Select(u => u.Id).ShouldBe([member.Id, other.Id], ignoreOrder: true);
    }

    [Test]
    public async Task user_role_member_granted_read_users_can_get_another_user()
    {
        // Arrange
        var (member, other) = await AddTwoUsersAsync("User", "User",
            first => first.AddPermission(Permission.ReadUsers()));
        SignInAs(member);

        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Users.GetRecord(other.Id));

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await result.Content.ReadFromJsonAsync<UserDto>()).ShouldNotBeNull().Id.ShouldBe(other.Id);
    }

    [Test]
    public async Task user_role_member_granted_read_users_can_get_another_user_by_identifier()
    {
        // Arrange
        var (member, other) = await AddTwoUsersAsync("User", "User",
            first => first.AddPermission(Permission.ReadUsers()));
        SignInAs(member);

        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Users.GetByIdentifier(other.Identifier));

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await result.Content.ReadFromJsonAsync<UserDto>()).ShouldNotBeNull().Id.ShouldBe(other.Id);
    }

    [Test]
    public async Task member_with_read_users_gets_not_found_for_an_unknown_identifier()
    {
        // Arrange
        var (member, _) = await AddTwoUsersAsync("User", "User",
            first => first.AddPermission(Permission.ReadUsers()));
        SignInAs(member);

        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Users.GetByIdentifier($"unknown-{Guid.NewGuid()}"));

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await result.Content.ReadFromJsonAsync<ProblemDetails>()).ShouldNotBeNull().Detail.ShouldBe("User was not found.");
    }

    [Test]
    public async Task admin_without_read_users_cannot_list_the_users()
    {
        // Arrange
        var (admin, _) = await AddTwoUsersAsync("Admin", "User",
            first => first.RemovePermission(Permission.ReadUsers()));
        SignInAs(admin);

        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Users.GetList);

        // Assert
        await ShouldBeForbiddenAsync(result, ReadUsersDenial);
    }

    [Test]
    public async Task read_only_machine_client_cannot_read_the_users()
    {
        // Arrange
        var (_, other) = await AddTwoUsersAsync("Admin", "User");
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object>
        {
            ["client_id"] = TestingWebApplicationFactory.ReadOnlyMachineClientId
        });

        // Act
        var list = await FactoryClient.GetRequestAsync(ApiRoutes.Users.GetList);
        var record = await FactoryClient.GetRequestAsync(ApiRoutes.Users.GetRecord(other.Id));

        // Assert
        const string denial = "This client has no permissions. It can read only the data that needs no permission.";
        await ShouldBeForbiddenAsync(list, denial);
        await ShouldBeForbiddenAsync(record, denial);
    }

    private void SignInAs(User user)
        => FactoryClient.SignInAsAppUser(user.Identifier);

    private static async Task ShouldBeForbiddenAsync(HttpResponseMessage result, string detail)
    {
        result.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var problem = (await result.Content.ReadFromJsonAsync<ProblemDetails>()).ShouldNotBeNull();
        problem.Status.ShouldBe(StatusCodes.Status403Forbidden);
        problem.Detail.ShouldBe(detail);
    }

    private static async Task<(User First, User Second)> AddTwoUsersAsync(
        string firstRole, string secondRole, Action<User>? changeFirst = null)
    {
        TestGraph.Start(out _)
            .AddTenant(out var tenant)
            .AddUsers(out var users, u => u.Role = firstRole, u => u.Role = secondRole);
        changeFirst?.Invoke(users[0]);
        await InsertAsync(tenant);
        foreach (var user in users)
            await InsertAsync(user);
        return (users[0], users[1]);
    }
}
