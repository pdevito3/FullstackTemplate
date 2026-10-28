namespace FullstackTemplate.IntegrationTests.DomainEvents;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.DomainEvents;
using FullstackTemplate.Server.Domain.Users.Features;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using static DomainEventTestServices;

/// <summary>
/// A handler of <see cref="UserDeleted"/> stands in for a domain that links to the user. It adds a row, so a second
/// commit of its change shows as a second row.
/// </summary>
public class UserDeletedTests : TestBase
{
    [Test]
    public async Task a_user_deleted_handler_change_commits_with_the_delete()
    {
        // Arrange
        var linkIdentifier = $"link-{Guid.NewGuid()}";
        string? tenantId = null;
        string? eventUserId = null;
        var foundInHandler = false;
        await using var services = TestFixture.BuildServiceProvider(s => s.AddTestHandler<UserDeleted>(async (e, db, ct) =>
        {
            eventUserId = e.UserId;
            foundInHandler = await db.Users.AnyAsync(u => u.Id == e.UserId, ct);
            db.Users.Add(NewUser(tenantId!, linkIdentifier));
        }));
        await new TestingServiceScope().StartGraph(out _).AddUser(out var user, u => u.Role = "User").BuildAsync();
        tenantId = user.TenantId;

        // Act
        await new TestingServiceScope(services).SendAsync(new DeleteUser.Command(user.Id));

        // Assert
        eventUserId.ShouldBe(user.Id);
        foundInHandler.ShouldBeTrue();
        (await DeletedAsync(user.Id)).ShouldBeTrue();
        (await LinkCountAsync(linkIdentifier)).ShouldBe(1);
    }

    [Test]
    public async Task a_user_deleted_handler_failure_keeps_the_user()
    {
        // Arrange
        await using var services = TestFixture.BuildServiceProvider(s => s.AddTestHandler<UserDeleted>((_, _, _) =>
            throw new InvalidOperationException("The link did not change.")));
        await new TestingServiceScope().StartGraph(out _).AddUser(out var user, u => u.Role = "User").BuildAsync();

        // Act
        var act = () => new TestingServiceScope(services).SendAsync(new DeleteUser.Command(user.Id));

        // Assert
        (await act.ShouldThrowAsync<InvalidOperationException>()).Message.ShouldBe("The link did not change.");
        (await DeletedAsync(user.Id)).ShouldBeFalse();
    }

    [Test]
    public async Task a_save_refuses_a_user_removed_without_delete()
    {
        // Arrange
        var scope = new TestingServiceScope();
        await scope.StartGraph(out _).AddUser(out var user, u => u.Role = "User").BuildAsync();

        // Act
        var act = () => scope.ExecuteDbContextAsync(async db =>
        {
            db.Users.Remove(await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == user.Id));
            return await db.SaveChangesAsync();
        });

        // Assert
        (await act.ShouldThrowAsync<InvalidOperationException>()).Message
            .ShouldBe("Remove a user with Remove(user.Delete()), so that the save queues UserDeleted.");
        (await DeletedAsync(user.Id)).ShouldBeFalse();
    }

    [Test]
    public async Task a_user_delete_that_runs_again_after_a_lost_race_commits_the_handler_change_one_time()
    {
        // Arrange
        var linkIdentifier = $"link-{Guid.NewGuid()}";
        string? tenantId = null;
        var runs = 0;
        var pause = PauseAtQuery.After("users.delete.load");
        await using var services = TestFixture.BuildServiceProvider(s => s
            .ConfigureDbContext<AppDbContext>((_, options) => options.AddInterceptors(pause))
            .AddTestHandler<UserDeleted>((_, db, _) =>
            {
                Interlocked.Increment(ref runs);
                db.Users.Add(NewUser(tenantId!, linkIdentifier));
                return Task.CompletedTask;
            }));
        await new TestingServiceScope().StartGraph(out _).AddUser(out var user, u => u.Role = "User").BuildAsync();
        tenantId = user.TenantId;

        // Act
        var delete = new TestingServiceScope(services).SendAsync(new DeleteUser.Command(user.Id));
        await pause.ReachedAsync();
        await new TestingServiceScope().SendAsync(new AddUserPermission.Command(user.Id, Permission.Names.ReadUsers));
        pause.Release();
        await delete;

        // Assert
        runs.ShouldBe(2);
        (await DeletedAsync(user.Id)).ShouldBeTrue();
        (await LinkCountAsync(linkIdentifier)).ShouldBe(1);
    }

    [Test]
    public async Task a_tenant_delete_raises_one_user_deleted_for_each_user_and_commits_each_handler_change()
    {
        // Arrange
        var linkPrefix = $"link-{Guid.NewGuid():N}";
        string? tenantId = null;
        var eventUserIds = new List<string>();
        await using var services = TestFixture.BuildServiceProvider(s => s.AddTestHandler<UserDeleted>((e, db, _) =>
        {
            eventUserIds.Add(e.UserId);
            db.Users.Add(NewUser(tenantId!, $"{linkPrefix}-{e.UserId}"));
            return Task.CompletedTask;
        }));
        var scope = new TestingServiceScope(services);
        var scenario = await scope.StartTenantScenarioAsync();
        tenantId = scenario.Tenant.Id;
        string[] userIds = [scenario.Admin.Id, scenario.FirstUser.Id, scenario.SecondUser.Id];

        // Act
        await scope.SendAsync(new DeleteTenant.Command(scenario.Tenant.Id));

        // Assert
        eventUserIds.ShouldBe(userIds, ignoreOrder: true);
        foreach (var userId in userIds)
        {
            (await DeletedAsync(userId)).ShouldBeTrue();
        }
        (await LinkCountAsync(linkPrefix)).ShouldBe(userIds.Length);
    }

    [Test]
    public async Task a_user_deleted_handler_failure_keeps_the_tenant_and_its_users()
    {
        // Arrange
        await using var services = TestFixture.BuildServiceProvider(s => s.AddTestHandler<UserDeleted>((_, _, _) =>
            throw new InvalidOperationException("The link did not change.")));
        var scope = new TestingServiceScope(services);
        var scenario = await scope.StartTenantScenarioAsync();

        // Act
        var act = () => scope.SendAsync(new DeleteTenant.Command(scenario.Tenant.Id));

        // Assert
        (await act.ShouldThrowAsync<InvalidOperationException>()).Message.ShouldBe("The link did not change.");
        (await ReadAsync(db => db.Tenants.IgnoreQueryFilters().Where(t => t.Id == scenario.Tenant.Id)
            .Select(t => t.IsDeleted).SingleAsync())).ShouldBeFalse();
        foreach (var user in new[] { scenario.Admin, scenario.FirstUser, scenario.SecondUser })
        {
            (await DeletedAsync(user.Id)).ShouldBeFalse();
        }
    }

    [Test]
    public async Task a_tenant_delete_that_runs_again_after_a_lost_race_commits_each_handler_change_one_time()
    {
        // Arrange
        var linkPrefix = $"link-{Guid.NewGuid():N}";
        string? tenantId = null;
        var runs = 0;
        var pause = PauseAtQuery.After("tenants.delete.load_users");
        await using var services = TestFixture.BuildServiceProvider(s => s
            .ConfigureDbContext<AppDbContext>((_, options) => options.AddInterceptors(pause))
            .AddTestHandler<UserDeleted>((e, db, _) =>
            {
                Interlocked.Increment(ref runs);
                db.Users.Add(NewUser(tenantId!, $"{linkPrefix}-{e.UserId}"));
                return Task.CompletedTask;
            }));
        var underTest = new TestingServiceScope(services);
        var scenario = await underTest.StartTenantScenarioAsync();
        tenantId = scenario.Tenant.Id;
        var rival = new TestingServiceScope();
        rival.SetUserWithDefaults(identifier: scenario.Admin.Identifier);

        // Act
        var delete = underTest.SendAsync(new DeleteTenant.Command(scenario.Tenant.Id));
        await pause.ReachedAsync();
        await rival.SendAsync(new AddUserPermission.Command(scenario.FirstUser.Id, Permission.Names.ManageUsers));
        pause.Release();
        await delete;

        // Assert
        runs.ShouldBe(6);
        (await DeletedAsync(scenario.FirstUser.Id)).ShouldBeTrue();
        (await LinkCountAsync(linkPrefix)).ShouldBe(3);
    }

    private static Task<int> LinkCountAsync(string prefix)
        => ReadAsync(db => db.Users.CountAsync(u => u.Identifier.StartsWith(prefix)));

    private static Task<bool> DeletedAsync(string userId)
        => ReadAsync(db => db.Users.IgnoreQueryFilters().Where(u => u.Id == userId).Select(u => u.IsDeleted).SingleAsync());
}
