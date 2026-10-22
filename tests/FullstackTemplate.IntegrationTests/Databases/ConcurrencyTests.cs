namespace FullstackTemplate.IntegrationTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.Tenants.Dtos;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Domain.Tenants.Models;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.Server.Resources.Entrypoints;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Shouldly;

/// <summary>
/// Each row carries its Postgres xmin as a version. A save of a row that changed after the load is a conflict, not a
/// lost update.
/// </summary>
public class ConcurrencyTests : TestBase
{
    [Test]
    public async Task save_of_a_tenant_that_changed_after_the_load_is_a_stale_version()
    {
        // Arrange
        var (first, second, tenantId) = await TwoScopesForANewTenantAsync();
        var firstTenant = await first.GetService<AppDbContext>().Tenants.GetById(tenantId);
        var secondTenant = await second.GetService<AppDbContext>().Tenants.GetById(tenantId);
        firstTenant.Update(new TenantForUpdate { Name = $"Tenant {Guid.NewGuid():N}", Version = firstTenant.Version });
        secondTenant.Update(new TenantForUpdate { Name = $"Tenant {Guid.NewGuid():N}", Version = secondTenant.Version });
        await first.GetService<AppDbContext>().SaveChangesAsync();

        // Act
        Func<Task> act = () => second.GetService<AppDbContext>().SaveChangesAsync();

        // Assert
        var exception = await act.ShouldThrowAsync<StaleVersionException>();
        DomainProblemDetails.StatusCodeFor(exception).ShouldBe(StatusCodes.Status409Conflict);
    }

    [Test]
    public async Task two_updates_from_one_loaded_version_give_one_success_and_one_stale_version()
    {
        // Arrange
        var (first, second, tenantId) = await TwoScopesForANewTenantAsync();
        var loaded = await first.SendAsync(new GetTenant.Query(tenantId));

        // Act
        var outcomes = await Task.WhenAll(
            OutcomeOf(first.SendAsync(new UpdateTenant.Command(tenantId,
                new TenantForUpdateDto { Name = $"Tenant {Guid.NewGuid():N}", Version = loaded.Version }))),
            OutcomeOf(second.SendAsync(new UpdateTenant.Command(tenantId,
                new TenantForUpdateDto { Name = $"Tenant {Guid.NewGuid():N}", Version = loaded.Version }))));

        // Assert
        outcomes.Count(o => o is null).ShouldBe(1);
        outcomes.Count(o => o is StaleVersionException).ShouldBe(1);
    }

    [Test]
    public async Task child_change_of_a_user_that_changed_after_the_load_is_a_stale_version()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddUser(out var user, u => u.Role = "User")
            .BuildAsync();
        var first = new TestingServiceScope();
        var second = new TestingServiceScope();
        var firstUser = await LoadWithPermissionsAsync(first, user.Id);
        var secondUser = await LoadWithPermissionsAsync(second, user.Id);
        AddPermission(first, firstUser);
        AddPermission(second, secondUser);
        await first.GetService<AppDbContext>().SaveChangesAsync();

        // Act
        Func<Task> act = () => second.GetService<AppDbContext>().SaveChangesAsync();

        // Assert
        firstUser.Version.ShouldNotBe(user.Version);
        await act.ShouldThrowAsync<StaleVersionException>();
        var saved = await LoadWithPermissionsAsync(new TestingServiceScope(), user.Id);
        saved.UserPermissions.Count.ShouldBe(1);
    }

    [Test]
    public async Task child_change_of_a_user_that_is_not_tracked_is_refused()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddUser(out var user, u => u.Role = "User")
            .BuildAsync();
        var dbContext = testingServiceScope.GetService<AppDbContext>();
        var untrackedUser = await dbContext.Users.AsNoTracking().GetById(user.Id);
        dbContext.UserPermissions.Add(UserPermission.Create(untrackedUser, Permission.DoSomethingSpecial()));

        // Act
        Func<Task> act = () => dbContext.SaveChangesAsync();

        // Assert
        var exception = await act.ShouldThrowAsync<InvalidOperationException>();
        exception.Message.ShouldContain("Load the User");
    }

    [Test]
    public async Task updated_tenant_carries_its_new_version()
    {
        // Arrange
        var (scope, _, tenantId) = await TwoScopesForANewTenantAsync();
        var loaded = await scope.SendAsync(new GetTenant.Query(tenantId));

        // Act
        var updated = await scope.SendAsync(new UpdateTenant.Command(tenantId,
            new TenantForUpdateDto { Name = $"Tenant {Guid.NewGuid():N}", Version = loaded.Version }));

        // Assert
        updated.Version.ShouldNotBe(0u);
        updated.Version.ShouldNotBe(loaded.Version);
        var reloaded = await new TestingServiceScope().ExecuteDbContextAsync(db =>
            db.Tenants.IgnoreQueryFilters().SingleAsync(t => t.Id == tenantId));
        reloaded.Version.ShouldBe(updated.Version);
    }

    // A new tenant with an admin in it, and two scopes that act as that admin
    private static async Task<(TestingServiceScope First, TestingServiceScope Second, string TenantId)> TwoScopesForANewTenantAsync()
    {
        await new TestingServiceScope().StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUser(out var admin, u => u.Role = "Admin")
            .BuildAsync();
        var first = new TestingServiceScope();
        var second = new TestingServiceScope();
        first.SetUserWithDefaults(identifier: admin.Identifier);
        second.SetUserWithDefaults(identifier: admin.Identifier);
        return (first, second, tenant.Id);
    }

    // Null when the update succeeded
    private static async Task<Exception?> OutcomeOf(Task<TenantDto> update)
    {
        try
        {
            await update;
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static Task<User> LoadWithPermissionsAsync(TestingServiceScope scope, string userId)
        => scope.GetService<AppDbContext>().Users.Include(u => u.UserPermissions).GetById(userId);

    private static void AddPermission(TestingServiceScope scope, User user)
    {
        user.AddPermission(Permission.DoSomethingSpecial());
        scope.GetService<AppDbContext>().UserPermissions.Add(user.UserPermissions.Single());
    }
}
