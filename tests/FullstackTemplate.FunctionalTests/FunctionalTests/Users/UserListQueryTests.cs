namespace FullstackTemplate.FunctionalTests.FunctionalTests.Users;

using System.Net;
using System.Net.Http.Json;
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
    [Theory]
    [InlineData("FirstName == \"{firstName}\"")]
    [InlineData("LastName == \"{lastName}\"")]
    [InlineData("FullName == \"{firstName} {lastName}\"")]
    [InlineData("Email == \"{email}\"")]
    [InlineData("Username == \"{username}\"")]
    [InlineData("Identifier == \"{identifier}\"")]
    [InlineData("Id == \"{id}\"")]
    [InlineData("Role == \"{role}\"")]
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
            .Replace("{role}", target.Role.Value);

        // Act
        var result = await FactoryClient.GetRequestAsync($"{ApiRoutes.Users.GetList}?filters={Uri.EscapeDataString(filters)}");

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var users = (await result.Content.ReadFromJsonAsync<List<UserDto>>()).ShouldNotBeNull();
        users.ShouldHaveSingleItem().Id.ShouldBe(target.Id);
    }

    [Theory]
    [InlineData("FirstName")]
    [InlineData("LastName desc")]
    [InlineData("FullName")]
    [InlineData("Email desc")]
    [InlineData("Username")]
    [InlineData("Role")]
    public async Task sort_on_a_dto_field_returns_the_users(string sortOrder)
    {
        // Arrange
        await ArrangeTwoUsersAsync();

        // Act
        var result = await FactoryClient.GetRequestAsync($"{ApiRoutes.Users.GetList}?sortOrder={Uri.EscapeDataString(sortOrder)}");

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await result.Content.ReadFromJsonAsync<List<UserDto>>()).ShouldNotBeNull().Count.ShouldBe(2);
    }

    // The DTO does not show these fields, so a caller must not filter or sort on them.
    // A 400 here also shows that the error message names only a field that the contract does not have.
    [Theory]
    [InlineData("filters", "CreatedBy == \"x\"")]
    [InlineData("filters", "LastModifiedBy == \"x\"")]
    [InlineData("filters", "IsDeleted == true")]
    [InlineData("filters", "UserPermissions.Count > 0")]
    [InlineData("filters", "(CreatedBy, FirstName) @= \"x\"")]
    [InlineData("sortOrder", "CreatedOn desc")]
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

    // Makes a new tenant with two users, signs in as the second user, and returns the first user.
    // The first user is an admin and the second is not, so a filter on the role matches only the first user.
    private async Task<User> ArrangeTwoUsersAsync()
    {
        var tenant = Tenant.Create(new FakeTenantForCreation().Generate());
        await InsertAsync(tenant);
        var target = User.Create(new FakeUserForCreation().RuleFor(u => u.TenantId, tenant.Id).Generate());
        target.UpdateRole(UserRole.Admin());
        var caller = User.Create(new FakeUserForCreation().RuleFor(u => u.TenantId, tenant.Id).Generate());
        caller.UpdateRole(UserRole.User());
        await InsertAsync(target, caller);
        FactoryClient.SetFakeBearerToken(new Dictionary<string, object> { ["sub"] = caller.Identifier });
        return target;
    }
}
