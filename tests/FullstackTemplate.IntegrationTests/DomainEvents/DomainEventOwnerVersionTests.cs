namespace FullstackTemplate.IntegrationTests.DomainEvents;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.Tenants.DomainEvents;
using FullstackTemplate.Server.Domain.Tenants.Dtos;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.Dtos;
using FullstackTemplate.Server.Domain.Users.Features;
using FullstackTemplate.Server.Domain.Users.Models;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.Server.Resources.Entrypoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using static DomainEventTestServices;

/// <summary>
/// A handler that changes a child of an aggregate changes the version of its root, the same as a change through the
/// root outside a handler. A handler runs before the save checks the owner versions, so the check sees its changes.
/// </summary>
public class DomainEventOwnerVersionTests : TestBase
{
    [Test]
    public async Task handler_that_adds_a_child_changes_the_version_of_its_root()
    {
        // Arrange
        string? userId = null;
        await using var services = TestFixture.BuildServiceProvider(s => s.AddTestHandler<TenantUpdated>(
            async (_, db, ct) => AddPermission(db, await LoadWithPermissionsAsync(db, userId!, ct))));
        var scope = new TestingServiceScope(services);
        var scenario = await scope.StartTenantScenarioAsync();
        userId = scenario.FirstUser.Id;
        var loadedVersion = scenario.FirstUser.Version;

        // Act
        await scope.SendAsync(new UpdateTenant.Command(scenario.Tenant.Id,
            new TenantForUpdateDto { Name = $"renamed-{Guid.NewGuid()}", Version = scenario.Tenant.Version }));

        // Assert
        var saved = await ReadAsync(read => read.Users.Include(u => u.UserPermissions).SingleAsync(u => u.Id == userId));
        saved.UserPermissions.ShouldHaveSingleItem();
        saved.Version.ShouldNotBe(loadedVersion);
        var later = new TestingServiceScope(services);
        later.SetUserWithDefaults(identifier: scenario.Admin.Identifier);
        var staleUpdate = () => later.SendAsync(new UpdateUser.Command(userId,
            new UserForUpdateDto
            {
                FirstName = "Stale",
                LastName = saved.LastName,
                Email = saved.Email.Value,
                Username = saved.Username,
                Version = loadedVersion,
            }));
        var exception = await staleUpdate.ShouldThrowAsync<StaleVersionException>();
        DomainProblemDetails.StatusCodeFor(exception).ShouldBe(StatusCodes.Status409Conflict);
    }

    [Test]
    public async Task save_of_a_root_loaded_before_a_handler_added_its_child_is_a_stale_version()
    {
        // Arrange
        string? userId = null;
        await using var services = TestFixture.BuildServiceProvider(s => s.AddTestHandler<TenantUpdated>(
            async (_, db, ct) => AddPermission(db, await LoadWithPermissionsAsync(db, userId!, ct))));
        var scope = new TestingServiceScope(services);
        var scenario = await scope.StartTenantScenarioAsync();
        userId = scenario.FirstUser.Id;
        var staleScope = new TestingServiceScope(services);
        staleScope.SetUserWithDefaults(identifier: scenario.Admin.Identifier);
        var staleDb = staleScope.GetService<AppDbContext>();
        var staleUser = await staleDb.Users.GetById(userId);
        await scope.SendAsync(new UpdateTenant.Command(scenario.Tenant.Id,
            new TenantForUpdateDto { Name = $"renamed-{Guid.NewGuid()}", Version = scenario.Tenant.Version }));
        staleUser.Update(new UserForUpdate
        {
            FirstName = "Stale",
            LastName = staleUser.LastName,
            Email = staleUser.Email.Value,
            Username = staleUser.Username,
            Version = staleUser.Version,
        });

        // Act
        var act = () => staleDb.SaveChangesAsync();

        // Assert
        var exception = await act.ShouldThrowAsync<StaleVersionException>();
        DomainProblemDetails.StatusCodeFor(exception).ShouldBe(StatusCodes.Status409Conflict);
        (await ReadAsync(read => read.Users.Where(u => u.Id == userId).Select(u => u.FirstName).SingleAsync()))
            .ShouldBe(scenario.FirstUser.FirstName);
    }

    [Test]
    public async Task handler_that_adds_a_child_of_an_untracked_root_fails_the_save_and_saves_nothing()
    {
        // Arrange
        string? userId = null;
        await using var services = TestFixture.BuildServiceProvider(s => s.AddTestHandler<TenantUpdated>(
            async (_, db, ct) =>
            {
                var untrackedUser = await db.Users.AsNoTracking().SingleAsync(u => u.Id == userId, ct);
                db.UserPermissions.Add(UserPermission.Create(untrackedUser, Permission.ManageUsers()));
            }));
        var scope = new TestingServiceScope(services);
        var scenario = await scope.StartTenantScenarioAsync();
        userId = scenario.FirstUser.Id;

        // Act
        var act = () => scope.SendAsync(new UpdateTenant.Command(scenario.Tenant.Id,
            new TenantForUpdateDto { Name = $"renamed-{Guid.NewGuid()}", Version = scenario.Tenant.Version }));

        // Assert
        (await act.ShouldThrowAsync<InvalidOperationException>()).Message.ShouldBe(
            $"The User '{userId}' owns a UserPermission in an aggregate that changed, but the User is not tracked. Load the User, then change the UserPermission through it.");
        (await ReadAsync(read => read.Tenants.Where(t => t.Id == scenario.Tenant.Id).Select(t => t.Name).SingleAsync()))
            .ShouldBe(scenario.Tenant.Name);
        (await ReadAsync(read => read.UserPermissions.CountAsync(p => p.UserId == userId))).ShouldBe(0);
        var saveAgain = () => scope.GetService<AppDbContext>().SaveChangesAsync();
        (await saveAgain.ShouldThrowAsync<InvalidOperationException>()).Message.ShouldContain("Use a new DI scope");
    }

    private static Task<User> LoadWithPermissionsAsync(AppDbContext db, string userId, CancellationToken ct)
        => db.Users.Include(u => u.UserPermissions).SingleAsync(u => u.Id == userId, ct);

    // A new permission is not tracked through the collection, because the navigation uses its field
    private static void AddPermission(AppDbContext db, User user)
    {
        user.AddPermission(Permission.ManageUsers());
        db.UserPermissions.Add(user.UserPermissions.Single());
    }
}
