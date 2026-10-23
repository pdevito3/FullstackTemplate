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
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
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
    public async Task two_updates_from_one_loaded_version_give_one_success_and_one_stale_version()
    {
        // Arrange
        var (first, second, tenantId) = await TwoAdminScopesForANewTenantAsync();
        var loaded = await first.SendAsync(new GetTenant.Query(tenantId));

        // Act
        var outcomes = await Task.WhenAll(
            FailureOf(first.SendAsync(new UpdateTenant.Command(tenantId,
                new TenantForUpdateDto { Name = $"Tenant {Guid.NewGuid():N}", Version = loaded.Version }))),
            FailureOf(second.SendAsync(new UpdateTenant.Command(tenantId,
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
    public async Task two_permission_adds_from_one_loaded_version_give_one_success_and_one_stale_version()
    {
        // Arrange
        await new TestingServiceScope().StartGraph(out _)
            .AddUser(out var user, u => u.Role = "User")
            .BuildAsync();
        await using var rowLock = await LockUserRowAsync(user.Id);

        // Act
        Task<Exception?>[] adds =
        [
            FailureOf(new TestingServiceScope().SendAsync(new AddUserPermission.Command(user.Id, "manage_users"))),
            FailureOf(new TestingServiceScope().SendAsync(new AddUserPermission.Command(user.Id, "manage_users"))),
        ];
        await WaitUntilBlockedAsync(rowLock.ProcessID, adds);
        await rowLock.CloseAsync();
        var outcomes = await Task.WhenAll(adds);

        // Assert
        outcomes.Count(o => o is null).ShouldBe(1);
        outcomes.Count(o => o is StaleVersionException).ShouldBe(1);
        var saved = await LoadWithPermissionsAsync(new TestingServiceScope(), user.Id);
        saved.UserPermissions.ShouldHaveSingleItem().Permission.Value.ShouldBe("manage_users");
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

    private static async Task<(TestingServiceScope First, TestingServiceScope Second, string TenantId)> TwoAdminScopesForANewTenantAsync()
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

    private static async Task<Exception?> FailureOf<T>(Task<T> update)
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

    // A FOR UPDATE lock on the user row does not stop a read, so a save blocks on it only after its handler loaded the
    // user. While it holds, each handler loads the same version. The connection is outside EF, because the retrying
    // execution strategy refuses a transaction that the caller starts.
    private static async Task<NpgsqlConnection> LockUserRowAsync(string userId)
    {
        var connection = new NpgsqlConnection(
            new TestingServiceScope().GetService<IConfiguration>().GetConnectionString(DatabaseConsts.DatabaseName));
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        await using var command = new NpgsqlCommand("SELECT 1 FROM users WHERE id = @id FOR UPDATE", connection, transaction);
        command.Parameters.AddWithValue("id", userId);
        await command.ExecuteNonQueryAsync();
        return connection;
    }

    private static async Task WaitUntilBlockedAsync(int lockPid, Task<Exception?>[] saves)
    {
        var database = new TestingServiceScope().GetService<AppDbContext>().Database;
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            var blocked = (await database.SqlQuery<int>($"""
                WITH RECURSIVE blocked(pid) AS (
                    SELECT pid FROM pg_stat_activity WHERE {lockPid} = ANY(pg_blocking_pids(pid))
                    UNION
                    SELECT a.pid FROM pg_stat_activity a JOIN blocked b ON b.pid = ANY(pg_blocking_pids(a.pid))
                )
                SELECT count(*)::int AS "Value" FROM blocked
                """).ToListAsync()).Single();
            if (blocked == saves.Length)
                return;
            if (saves.FirstOrDefault(s => s.IsCompleted) is { } done)
                throw new InvalidOperationException($"A save finished before the lock was released: {await done}");
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"{blocked} of {saves.Length} saves waited on the lock.");
            await Task.Delay(20);
        }
    }

    private static Task<User> LoadWithPermissionsAsync(TestingServiceScope scope, string userId)
        => scope.GetService<AppDbContext>().Users.Include(u => u.UserPermissions).GetById(userId);

    private static void AddPermission(TestingServiceScope scope, User user)
    {
        user.AddPermission(Permission.ManageUsers());
        scope.GetService<AppDbContext>().UserPermissions.Add(user.UserPermissions.Single());
    }
}
