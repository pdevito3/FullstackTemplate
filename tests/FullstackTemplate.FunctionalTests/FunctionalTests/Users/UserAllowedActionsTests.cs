namespace FullstackTemplate.FunctionalTests.FunctionalTests.Users;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.Dtos;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using Shouldly;
using TestUtilities;

/// <summary>
/// The user record says which actions the caller can send for it now, so the SPA shows a button without a copy of
/// the permission rules.
/// </summary>
public class UserAllowedActionsTests : TestBase
{
    [Test]
    public async Task admin_gets_each_action_for_another_user()
    {
        // Arrange
        var (admin, other) = await AddTwoUsersAsync("Admin", "User");
        SignInAs(admin);

        // Act
        var allowedActions = await GetAllowedActionsAsync(other.Id);

        // Assert
        allowedActions.ShouldBe(["Update", "UpdateRole", "AddPermission", "RemovePermission", "Delete"]);
    }

    [Test]
    public async Task reader_with_no_other_permission_gets_no_action_for_another_user()
    {
        // Arrange
        var (reader, other) = await AddTwoUsersAsync("User", "User",
            first => first.AddPermission(Permission.ReadUsers()));
        SignInAs(reader);

        // Act
        var allowedActions = await GetAllowedActionsAsync(other.Id);

        // Assert
        allowedActions.ShouldBeEmpty();
    }

    [Test]
    public async Task member_with_no_permission_can_only_update_their_own_record()
    {
        // Arrange
        var (member, _) = await AddTwoUsersAsync("User", "User");
        SignInAs(member);

        // Act
        var allowedActions = await GetAllowedActionsAsync(member.Id);

        // Assert
        allowedActions.ShouldBe(["Update"]);
    }

    [Test]
    public async Task each_row_of_the_list_has_the_actions_of_the_caller()
    {
        // Arrange
        var (manager, admin) = await AddTwoUsersAsync("User", "Admin",
            first => first.AddPermissions([Permission.ManageUsers(), Permission.ReadUsers()]));
        SignInAs(manager);

        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Users.GetList);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var users = (await result.Content.ReadFromJsonAsync<List<UserDto>>()).ShouldNotBeNull();
        // The manager cannot take away the permissions of the admin, so the admin row has no Delete
        users.Single(u => u.Id == admin.Id).AllowedActions.ShouldBe(["Update"]);
        users.Single(u => u.Id == manager.Id).AllowedActions.ShouldBe(["Update", "Delete"]);
    }

    [Test]
    public async Task update_response_has_the_actions_for_the_permissions_of_the_user()
    {
        // Arrange
        var (manager, admin) = await AddTwoUsersAsync("User", "Admin",
            first => first.AddPermissions([Permission.ManageUsers(), Permission.ReadUsers()]));
        SignInAs(manager);
        var dto = new FakeUserForUpdateDto().Generate() with { Version = admin.Version };

        // Act
        var result = await FactoryClient.PutJsonRequestAsync(ApiRoutes.Users.Put(admin.Id), dto);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var user = (await result.Content.ReadFromJsonAsync<UserDto>()).ShouldNotBeNull();
        user.AllowedActions.ShouldBe(["Update"]);
    }

    [Test]
    public async Task admin_who_changes_their_own_role_gets_the_actions_of_the_new_role()
    {
        // Arrange
        var (admin, _) = await AddTwoUsersAsync("Admin", "User");
        SignInAs(admin);

        // Act
        var result = await FactoryClient.PutJsonRequestAsync(ApiRoutes.Users.UpdateRole(admin.Id), new { role = "User" });

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var user = (await result.Content.ReadFromJsonAsync<UserDto>()).ShouldNotBeNull();
        user.AllowedActions.ShouldBe(["Update"]);
    }

    private async Task<string[]> GetAllowedActionsAsync(string userId)
    {
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Users.GetRecord(userId));
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await result.Content.ReadAsStringAsync());
        body.RootElement.TryGetProperty("allowedActions", out var allowedActions)
            .ShouldBeTrue("The user record has no allowedActions.");
        return allowedActions.EnumerateArray().Select(a => a.GetString()!).ToArray();
    }

    private void SignInAs(User user)
        => FactoryClient.SignInAsAppUser(user.Identifier);

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
