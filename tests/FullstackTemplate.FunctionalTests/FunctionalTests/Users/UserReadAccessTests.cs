namespace FullstackTemplate.FunctionalTests.FunctionalTests.Users;

using System.Net;
using System.Net.Http.Json;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.Dtos;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;
using TestUtilities;

/// <summary>
/// A user with the read_users permission can read the other users of the tenant. The permission row decides, not
/// the role. A user reads their own record with no permission. A command that returns another user needs it too.
/// </summary>
public class UserReadAccessTests : TestBase
{
    private const string ReadUsersDenial = "You cannot see the users of the tenant. Ask a tenant admin for access.";

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

    // A new user reads their own identifier to know that they must start a tenant
    [Test]
    public async Task user_with_no_user_record_gets_not_found_for_their_own_identifier()
    {
        // Arrange
        await AddTwoUsersAsync("Admin", "User");
        var identifier = $"new-{Guid.NewGuid()}";
        FactoryClient.SignInAsAppUser(identifier);

        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Users.GetByIdentifier(identifier));

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await result.Content.ReadFromJsonAsync<ProblemDetails>()).ShouldNotBeNull().Detail
            .ShouldBe("User was not found.");
    }

    // The delete skips the save, as a delete on another server instance does, so the tenant of the user stays cached
    [Test]
    public async Task deleted_user_with_a_cached_tenant_is_forbidden_for_an_unknown_identifier()
    {
        // Arrange
        var (member, _) = await AddTwoUsersAsync("User", "User");
        SignInAs(member);
        (await FactoryClient.GetRequestAsync(ApiRoutes.Users.GetRecord(member.Id))).StatusCode
            .ShouldBe(HttpStatusCode.OK);
        await DeleteOutsideTheServerAsync(member);

        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Users.GetByIdentifier($"unknown-{Guid.NewGuid()}"));

        // Assert
        await ShouldBeForbiddenAsync(result, "You need a user record in a tenant to do this.");
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

    // A command returns the record of the user, so a manage permission alone must not show another user
    [Test]
    public async Task member_with_only_manage_user_access_cannot_remove_a_permission_of_another_user()
    {
        // Arrange
        var (member, other) = await AddTwoUsersAsync("User", "User",
            first => first.AddPermission(Permission.ManageUserAccess()),
            second => second.AddPermission(Permission.ManageUserAccess()));
        SignInAs(member);

        // Act
        var result = await FactoryClient.DeleteRequestAsync(
            ApiRoutes.Users.RemovePermission(other.Id, Permission.Names.ManageUserAccess));

        // Assert
        await ShouldBeForbiddenAsync(result, ReadUsersDenial);
        (await PermissionsOfAsync(other.Id)).ShouldBe([Permission.Names.ManageUserAccess]);
    }

    [Test]
    public async Task member_with_only_manage_user_access_cannot_add_a_permission_to_another_user()
    {
        // Arrange
        var (member, other) = await AddTwoUsersAsync("User", "User",
            first => first.AddPermission(Permission.ManageUserAccess()));
        SignInAs(member);

        // Act
        var result = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Users.AddPermission(other.Id),
            new UserPermissionDto { Permission = Permission.Names.ManageUserAccess });

        // Assert
        await ShouldBeForbiddenAsync(result, ReadUsersDenial);
        (await PermissionsOfAsync(other.Id)).ShouldBeEmpty();
    }

    [Test]
    public async Task member_with_only_manage_user_access_cannot_change_the_role_of_another_user()
    {
        // Arrange
        var (member, other) = await AddTwoUsersAsync("User", "User",
            first => first.AddPermission(Permission.ManageUserAccess()),
            second => second.AddPermission(Permission.ManageUserAccess()));
        SignInAs(member);

        // Act
        var result = await FactoryClient.PutJsonRequestAsync(ApiRoutes.Users.UpdateRole(other.Id),
            new UpdateUserRoleDto { Role = "User" });

        // Assert
        await ShouldBeForbiddenAsync(result, ReadUsersDenial);
        (await PermissionsOfAsync(other.Id)).ShouldBe([Permission.Names.ManageUserAccess]);
    }

    [Test]
    public async Task member_with_only_manage_users_cannot_update_another_user()
    {
        // Arrange
        var (member, other) = await AddTwoUsersAsync("User", "User",
            first => first.AddPermission(Permission.ManageUsers()));
        SignInAs(member);

        // Act
        var result = await FactoryClient.PutJsonRequestAsync(ApiRoutes.Users.Put(other.Id),
            new FakeUserForUpdateDto().Generate() with { Version = other.Version });

        // Assert
        await ShouldBeForbiddenAsync(result, ReadUsersDenial);
        var stored = await ExecuteDbContextAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == other.Id));
        stored.FirstName.ShouldBe(other.FirstName);
        stored.Version.ShouldBe(other.Version);
    }

    [Test]
    public async Task member_with_only_manage_user_access_can_remove_their_own_permission()
    {
        // Arrange
        var (member, _) = await AddTwoUsersAsync("User", "User",
            first => first.AddPermission(Permission.ManageUserAccess()));
        SignInAs(member);

        // Act
        var result = await FactoryClient.DeleteRequestAsync(
            ApiRoutes.Users.RemovePermission(member.Id, Permission.Names.ManageUserAccess));

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await PermissionsOfAsync(member.Id)).ShouldBeEmpty();
    }

    private static Task<List<string>> PermissionsOfAsync(string userId)
        => ExecuteDbContextAsync(db => db.UserPermissions
            .Where(p => p.UserId == userId)
            .Select(p => p.Permission.Value)
            .ToListAsync());

    private void SignInAs(User user)
        => FactoryClient.SignInAsAppUser(user.Identifier);

    private static async Task ShouldBeForbiddenAsync(HttpResponseMessage result, string detail)
    {
        result.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var problem = (await result.Content.ReadFromJsonAsync<ProblemDetails>()).ShouldNotBeNull();
        problem.Status.ShouldBe(StatusCodes.Status403Forbidden);
        problem.Detail.ShouldBe(detail);
    }

    private static async Task DeleteOutsideTheServerAsync(User user)
    {
        var connectionString = await ExecuteScopeAsync(sp => Task.FromResult(
            sp.GetRequiredService<IConfiguration>().GetConnectionString(DatabaseConsts.DatabaseName)));
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("UPDATE users SET is_deleted = true WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", user.Id);
        (await command.ExecuteNonQueryAsync()).ShouldBe(1);
    }

    private static async Task<(User First, User Second)> AddTwoUsersAsync(
        string firstRole, string secondRole, Action<User>? changeFirst = null, Action<User>? changeSecond = null)
    {
        TestGraph.Start(out _)
            .AddTenant(out var tenant)
            .AddUsers(out var users, u => u.Role = firstRole, u => u.Role = secondRole);
        changeFirst?.Invoke(users[0]);
        changeSecond?.Invoke(users[1]);
        await InsertAsync(tenant);
        foreach (var user in users)
            await InsertAsync(user);
        return (users[0], users[1]);
    }
}
