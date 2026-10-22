namespace FullstackTemplate.FunctionalTests.FunctionalTests.Users;

using System.Net;
using System.Net.Http.Json;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.Dtos;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using TestUtilities;

/// <summary>
/// A user with the manage_users permission can delete a user and update any user of the tenant. The permission
/// row decides, not the role. A user can update their own record.
/// </summary>
public class UserManagementAccessTests : TestBase
{
    [Test]
    public async Task basic_member_cannot_delete_another_user()
    {
        // Arrange
        var (member, other) = await AddTwoUsersAsync("User", "User");
        SignInAs(member);

        // Act
        var result = await FactoryClient.DeleteRequestAsync(ApiRoutes.Users.Delete(other.Id));

        // Assert
        await ShouldBeForbiddenAsync(result);
        var exists = await ExecuteDbContextAsync(db => db.Users.AnyAsync(u => u.Id == other.Id));
        exists.ShouldBeTrue();
    }

    [Test]
    public async Task admin_can_delete_a_user_of_their_tenant()
    {
        // Arrange
        var (admin, other) = await AddTwoUsersAsync("Admin", "User");
        SignInAs(admin);

        // Act
        var result = await FactoryClient.DeleteRequestAsync(ApiRoutes.Users.Delete(other.Id));

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task admin_without_manage_users_gets_a_forbidden_problem_on_delete()
    {
        // Arrange
        var (admin, other) = await AddTwoUsersAsync("Admin", "User",
            first => first.RemovePermission(Permission.ManageUsers()));
        SignInAs(admin);

        // Act
        var result = await FactoryClient.DeleteRequestAsync(ApiRoutes.Users.Delete(other.Id));

        // Assert
        var problem = await ShouldBeForbiddenAsync(result);
        problem.Detail.ShouldBe("You need the manage_users permission to do this.");
        var exists = await ExecuteDbContextAsync(db => db.Users.AnyAsync(u => u.Id == other.Id));
        exists.ShouldBeTrue();
    }

    [Test]
    public async Task user_role_member_granted_manage_users_can_delete_a_user()
    {
        // Arrange
        var (member, other) = await AddTwoUsersAsync("User", "User",
            first => first.AddPermission(Permission.ManageUsers()));
        SignInAs(member);

        // Act
        var result = await FactoryClient.DeleteRequestAsync(ApiRoutes.Users.Delete(other.Id));

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task delete_with_no_token_is_unauthorized_not_forbidden()
    {
        // Arrange
        var (_, other) = await AddTwoUsersAsync("Admin", "User");

        // Act
        var result = await FactoryClient.DeleteRequestAsync(ApiRoutes.Users.Delete(other.Id));

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task basic_member_cannot_update_another_user()
    {
        // Arrange
        var (member, other) = await AddTwoUsersAsync("User", "User");
        SignInAs(member);

        // Act
        var result = await FactoryClient.PutJsonRequestAsync(
            ApiRoutes.Users.Put(other.Id), new FakeUserForUpdateDto().Generate() with { Version = other.Version });

        // Assert
        await ShouldBeForbiddenAsync(result);
        var firstName = await ExecuteDbContextAsync(db => db.Users
            .Where(u => u.Id == other.Id).Select(u => u.FirstName).SingleAsync());
        firstName.ShouldBe(other.FirstName);
    }

    [Test]
    public async Task basic_member_can_update_their_own_record()
    {
        // Arrange
        var (member, _) = await AddTwoUsersAsync("User", "User");
        SignInAs(member);
        var dto = new FakeUserForUpdateDto().Generate() with { Version = member.Version };

        // Act
        var result = await FactoryClient.PutJsonRequestAsync(ApiRoutes.Users.Put(member.Id), dto);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await result.Content.ReadFromJsonAsync<UserDto>()).ShouldNotBeNull().FirstName.ShouldBe(dto.FirstName);
    }

    [Test]
    public async Task admin_can_update_another_user_of_their_tenant()
    {
        // Arrange
        var (admin, other) = await AddTwoUsersAsync("Admin", "User");
        SignInAs(admin);
        var dto = new FakeUserForUpdateDto().Generate() with { Version = other.Version };

        // Act
        var result = await FactoryClient.PutJsonRequestAsync(ApiRoutes.Users.Put(other.Id), dto);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await result.Content.ReadFromJsonAsync<UserDto>()).ShouldNotBeNull().FirstName.ShouldBe(dto.FirstName);
    }

    [Test]
    public async Task read_only_machine_client_cannot_change_a_user()
    {
        // Arrange
        var (_, other) = await AddTwoUsersAsync("Admin", "User");
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object>
        {
            ["client_id"] = TestingWebApplicationFactory.ReadOnlyMachineClientId
        });

        // Act
        var updated = await FactoryClient.PutJsonRequestAsync(
            ApiRoutes.Users.Put(other.Id), new FakeUserForUpdateDto().Generate() with { Version = other.Version });
        var deleted = await FactoryClient.DeleteRequestAsync(ApiRoutes.Users.Delete(other.Id));

        // Assert
        await ShouldBeForbiddenAsync(updated);
        await ShouldBeForbiddenAsync(deleted);
    }

    private void SignInAs(User user)
        => FactoryClient.SetFakeBearerToken(new Dictionary<string, object> { ["sub"] = user.Identifier });

    private static async Task<ProblemDetails> ShouldBeForbiddenAsync(HttpResponseMessage result)
    {
        result.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var problem = (await result.Content.ReadFromJsonAsync<ProblemDetails>()).ShouldNotBeNull();
        problem.Status.ShouldBe(StatusCodes.Status403Forbidden);
        return problem;
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
