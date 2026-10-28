namespace FullstackTemplate.IntegrationTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.Tenants.Dtos;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Domain.Tenants.Models;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.Features;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.Server.Resources.Entrypoints;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Shouldly;

public class ConcurrencyTests : TestBase
{
    [Test]
    public async Task save_of_a_tenant_that_changed_after_the_load_is_a_stale_version()
    {
        // Arrange
        var (first, second, tenantId) = await TwoAdminScopesForANewTenantAsync();
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
    public async Task update_that_a_rival_update_beat_after_the_load_is_a_stale_version()
    {
        // Arrange
        var pause = PauseAtQuery.After("tenants.update.load");
        await using var services = pause.BuildServices();
        var (underTest, rival, tenantId) = await TwoAdminScopesForANewTenantAsync(services);
        var loaded = await rival.SendAsync(new GetTenant.Query(tenantId));
        var rivalName = $"Tenant {Guid.NewGuid():N}";

        // Act
        var update = underTest.SendAsync(new UpdateTenant.Command(tenantId,
            new TenantForUpdateDto { Name = $"Tenant {Guid.NewGuid():N}", Version = loaded.Version }));
        await pause.ReachedAsync();
        await rival.SendAsync(new UpdateTenant.Command(tenantId,
            new TenantForUpdateDto { Name = rivalName, Version = loaded.Version }));
        pause.Release();

        // Assert
        await Should.ThrowAsync<StaleVersionException>(update);
        var saved = await new TestingServiceScope().ExecuteDbContextAsync(db =>
            db.Tenants.IgnoreQueryFilters().SingleAsync(t => t.Id == tenantId));
        saved.Name.ShouldBe(rivalName);
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
        dbContext.UserPermissions.Add(UserPermission.Create(untrackedUser, Permission.ManageUsers()));

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
        var (scope, _, tenantId) = await TwoAdminScopesForANewTenantAsync();
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

    private static async Task<(TestingServiceScope UnderTest, TestingServiceScope Rival, string TenantId)> TwoAdminScopesForANewTenantAsync(
        IServiceProvider? underTestServices = null)
    {
        await new TestingServiceScope().StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUser(out var admin, u => u.Role = "Admin")
            .BuildAsync();
        var underTest = underTestServices is null ? new TestingServiceScope() : new TestingServiceScope(underTestServices);
        var rival = new TestingServiceScope();
        underTest.SetUserWithDefaults(identifier: admin.Identifier);
        rival.SetUserWithDefaults(identifier: admin.Identifier);
        return (underTest, rival, tenant.Id);
    }

    private static Task<User> LoadWithPermissionsAsync(TestingServiceScope scope, string userId)
        => scope.GetService<AppDbContext>().Users.Include(u => u.UserPermissions).GetById(userId);

    private static void AddPermission(TestingServiceScope scope, User user)
    {
        user.AddPermission(Permission.ManageUsers());
        scope.GetService<AppDbContext>().UserPermissions.Add(user.UserPermissions.Single());
    }
}
