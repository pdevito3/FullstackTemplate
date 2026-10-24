namespace FullstackTemplate.FunctionalTests.FunctionalTests.Users;

using System.Net;
using System.Net.Http.Json;
using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.Dtos;
using FullstackTemplate.SharedTestHelpers.Fakes.Tenant;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using TestUtilities;

public class UserListQueryTests : TestBase
{
    // Each filter matches the user that the test makes, and no other user in its tenant.
    [Test]
    [Arguments("FirstName == \"{firstName}\"")]
    [Arguments("LastName == \"{lastName}\"")]
    [Arguments("FullName == \"{firstName} {lastName}\"")]
    [Arguments("Email == \"{email}\"")]
    [Arguments("Username == \"{username}\"")]
    [Arguments("Identifier == \"{identifier}\"")]
    [Arguments("Id == \"{id}\"")]
    [Arguments("Role == \"{role}\"")]
    [Arguments("Permissions ^$ \"{permission}\"")]
    [Arguments("Permissions == \"{permission}\"")]
    public async Task filter_on_a_dto_field_returns_the_matching_user(string filterTemplate)
    {
        // Arrange
        var target = await ArrangeTwoUsersAsync();
        var filters = filterTemplate
            .Replace("{firstName}", target.FirstName)
            .Replace("{lastName}", target.LastName)
            .Replace("{email}", target.Email.Value)
            .Replace("{username}", target.Username)
            .Replace("{identifier}", target.Identifier)
            .Replace("{id}", target.Id.ToString())
            .Replace("{role}", target.Role.Value)
            .Replace("{permission}", Permission.ManageTenant().Value);

        // Act
        var result = await FactoryClient.GetRequestAsync($"{ApiRoutes.Users.GetList}?filters={Uri.EscapeDataString(filters)}");

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var users = (await result.Content.ReadFromJsonAsync<List<UserDto>>()).ShouldNotBeNull();
        users.ShouldHaveSingleItem().Id.ShouldBe(target.Id);
    }

    // The first user has the values that sort first, so an ascending sort returns the first user first.
    [Test]
    [Arguments("FirstName", true)]
    [Arguments("LastName desc", false)]
    [Arguments("FullName", true)]
    [Arguments("Email desc", false)]
    [Arguments("Username", true)]
    [Arguments("Role", true)]
    public async Task sort_on_a_dto_field_returns_the_users_in_order(string sortOrder, bool targetFirst)
    {
        // Arrange
        var target = await ArrangeTwoUsersAsync();

        // Act
        var result = await FactoryClient.GetRequestAsync($"{ApiRoutes.Users.GetList}?sortOrder={Uri.EscapeDataString(sortOrder)}");

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var users = (await result.Content.ReadFromJsonAsync<List<UserDto>>()).ShouldNotBeNull();
        users.Count.ShouldBe(2);
        (users[0].Id == target.Id).ShouldBe(targetFirst);
    }

    // The insert order and the Id order are both the reverse of the expected order, so neither can give the correct result
    [Test]
    public async Task list_with_no_sort_returns_the_users_by_last_name_then_first_name()
    {
        // Arrange
        var tenant = Tenant.Create(new FakeTenantForCreation().Generate());
        await InsertAsync(tenant);
        // The ID columns use the "C" collation, so PostgreSQL sorts the IDs in ordinal order
        var ids = new[] { EntityId.New(User.IdPrefix), EntityId.New(User.IdPrefix), EntityId.New(User.IdPrefix) }
            .Order(StringComparer.Ordinal)
            .ToArray();
        User UserNamed(string id, string lastName, string firstName) => User.Create(FakeUserInTenant(tenant.Id, "")
            .RuleFor(u => u.Id, id)
            .RuleFor(u => u.LastName, lastName)
            .RuleFor(u => u.FirstName, firstName)
            .Generate());
        var third = UserNamed(ids[0], "b", "a");
        var second = UserNamed(ids[1], "a", "b");
        var first = UserNamed(ids[2], "a", "a");
        await InsertAsync(third, second, first);
        FactoryClient.SignInAsAppUser(third.Identifier);

        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Users.GetList);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var users = (await result.Content.ReadFromJsonAsync<List<UserDto>>()).ShouldNotBeNull();
        users.Select(u => u.Id).ShouldBe([first.Id, second.Id, third.Id]);
    }

    // The users have the same role, so only the tie-break on Id puts each user on exactly one page.
    // Each insert is separate and in the reverse of the Id order, so the order of the rows on disk cannot give the correct result.
    // A sort on the names uses the index on the tenant and the names, which also has the Id, so it cannot show a missing tie-break.
    [Test]
    [Arguments("Role")]
    [Arguments("Role desc")]
    public async Task users_with_the_same_sort_value_each_show_on_one_page_in_id_order(string sortOrder)
    {
        // Arrange
        var tenant = Tenant.Create(new FakeTenantForCreation().Generate());
        await InsertAsync(tenant);
        var ids = Enumerable.Range(0, 5)
            .Select(_ => EntityId.New(User.IdPrefix))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var users = ids.Reverse()
            .Select(id => User.Create(FakeUserInTenant(tenant.Id, "")
                .RuleFor(u => u.Id, id)
                .RuleFor(u => u.Role, UserRole.User().Value)
                .Generate()))
            .ToArray();
        foreach (var user in users)
            await InsertAsync(user);
        FactoryClient.SignInAsAppUser(users[0].Identifier);

        // Act
        var pages = new List<string>();
        for (var pageNumber = 1; pageNumber <= ids.Length; pageNumber++)
        {
            var result = await FactoryClient.GetRequestAsync($"{ApiRoutes.Users.GetList}?pageNumber={pageNumber}&pageSize=1&sortOrder={Uri.EscapeDataString(sortOrder)}");
            result.StatusCode.ShouldBe(HttpStatusCode.OK);
            var page = (await result.Content.ReadFromJsonAsync<List<UserDto>>()).ShouldNotBeNull();
            pages.Add(page.ShouldHaveSingleItem().Id);
        }

        // Assert
        pages.ShouldBe(ids);
    }

    // The count of a list is a number, so a value that is not a number cannot convert
    [Test]
    public async Task filter_value_that_does_not_convert_returns_bad_request()
    {
        // Arrange
        await ArrangeTwoUsersAsync();

        // Act
        var result = await FactoryClient.GetRequestAsync($"{ApiRoutes.Users.GetList}?filters={Uri.EscapeDataString("Permissions #== \"abc\"")}");

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Errors.ShouldHaveSingleItem().Key.ShouldBe("filters");
    }

    // The DTO does not show these fields, so a caller must not filter or sort on them.
    // A 400 here also shows that the error message names only a field that the contract does not have.
    [Test]
    [Arguments("filters", "LastModifiedOn == \"x\"")]
    [Arguments("filters", "IsDeleted == true")]
    [Arguments("filters", "UserPermissions.Count > 0")]
    [Arguments("filters", "(LastModifiedOn, FirstName) @= \"x\"")]
    [Arguments("sortOrder", "LastModifiedOn desc")]
    public async Task query_on_a_field_that_the_dto_does_not_show_returns_bad_request(string parameter, string value)
    {
        // Arrange
        await ArrangeTwoUsersAsync();

        // Act
        var result = await FactoryClient.GetRequestAsync($"{ApiRoutes.Users.GetList}?{parameter}={Uri.EscapeDataString(value)}");

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Errors.ShouldHaveSingleItem().Key.ShouldBe(parameter);
    }

    // A version is opaque, so a list cannot filter or sort on it
    [Test]
    [Arguments("filters", "version > 5")]
    [Arguments("filters", "Version == 5")]
    [Arguments("sortOrder", "version")]
    public async Task query_on_the_version_returns_bad_request(string parameter, string value)
    {
        // Arrange
        await ArrangeTwoUsersAsync();

        // Act
        var result = await FactoryClient.GetRequestAsync($"{ApiRoutes.Users.GetList}?{parameter}={Uri.EscapeDataString(value)}");

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Errors.ShouldHaveSingleItem().Key.ShouldBe(parameter);
    }

    // Makes a new tenant with two users, signs in as the second user, and returns the first user.
    // The first user is an admin with a permission, and the second user is not an admin and has no permission.
    // So a filter on the role or on the permissions matches only the first user.
    // The values of the first user start with "a" and the values of the second user start with "b",
    // so each sort on a text field has one correct order in each collation.
    private async Task<User> ArrangeTwoUsersAsync()
    {
        var tenant = Tenant.Create(new FakeTenantForCreation().Generate());
        await InsertAsync(tenant);
        var target = User.Create(FakeUserInTenant(tenant.Id, "a").Generate());
        target.UpdateRole(UserRole.Admin());
        target.AddPermission(Permission.ManageTenant());
        var caller = User.Create(FakeUserInTenant(tenant.Id, "b").Generate());
        caller.UpdateRole(UserRole.User());
        await InsertAsync(target, caller);
        FactoryClient.SignInAsAppUser(caller.Identifier);
        return target;
    }

    private static FakeUserForCreation FakeUserInTenant(string tenantId, string prefix)
    {
        var faker = new FakeUserForCreation();
        faker.RuleFor(u => u.TenantId, tenantId)
            .RuleFor(u => u.FirstName, f => prefix + UserRules.FirstName(f))
            .RuleFor(u => u.LastName, f => prefix + UserRules.LastName(f))
            .RuleFor(u => u.Email, f => prefix + UserRules.Email(f))
            .RuleFor(u => u.Username, f => prefix + UserRules.Username(f));
        return faker;
    }
}
