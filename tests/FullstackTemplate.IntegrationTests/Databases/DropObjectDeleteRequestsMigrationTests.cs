namespace FullstackTemplate.IntegrationTests.Databases;

using BackWave.Monitor;
using FullstackTemplate.IntegrationTests.Jobs;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.Attachments;
using FullstackTemplate.Server.Domain.ObjectDeletes;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Resources.Jobs;
using FullstackTemplate.SharedTestHelpers.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NSubstitute;
using Shouldly;

/// <summary>
/// The drop of the old object delete queue runs on a database of its own, from the migration before it. A row in the
/// queue names an object that nothing else deletes. The start of the Server moves the rows onto BackWave before the
/// migration, and the migration refuses to drop a queue with rows.
/// </summary>
public sealed class DropObjectDeleteRequestsMigrationTests : IAsyncDisposable
{
    private const string PreviousMigration = "20261110223546_AddObjectDeleteRequests";
    private const string ThisMigration = "20261114082032_DropObjectDeleteRequests";

    private readonly PostgresTestDatabase _database = new();

    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    [Test]
    public async Task up_drops_an_empty_queue()
    {
        // Arrange
        await _database.StartAsync();
        await using var context = CreateContext();
        await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);

        // Act
        await context.GetService<IMigrator>().MigrateAsync(ThisMigration);

        // Assert
        (await TableExistsAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task up_refuses_a_queue_with_rows_and_keeps_them()
    {
        // Arrange
        await _database.StartAsync();
        await using var context = CreateContext();
        await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
        await ExecuteAsync("""
            INSERT INTO object_delete_requests (id, tenant_id, key, is_prefix, requested_at, due_at, attempts)
            VALUES ('odr_left', 'ten_left', 'tenants/ten_left/', true, now(), now(), 0);
            """);

        // Act
        var act = () => context.GetService<IMigrator>().MigrateAsync(ThisMigration);

        // Assert
        (await act.ShouldThrowAsync<PostgresException>()).MessageText.ShouldBe(
            "object_delete_requests has rows. Start the Server, which moves them onto BackWave before this migration.");
        (await TableExistsAsync()).ShouldBeTrue();
    }

    [Test]
    public async Task start_moves_each_row_of_the_queue_onto_backwave_and_drops_the_queue()
    {
        // Arrange
        await StartAtThePreviousMigrationAsync();
        var keyTenant = EntityId.New(Tenant.IdPrefix);
        var prefixTenant = EntityId.New(Tenant.IdPrefix);
        var key = $"{StoredFile.TenantPrefix(keyTenant)}attachments/live";
        var deadKey = $"{StoredFile.TenantPrefix(keyTenant)}attachments/dead";
        await AddRequestAsync("odr_key", keyTenant, key, isPrefix: false);
        await AddRequestAsync("odr_prefix", prefixTenant, StoredFile.TenantPrefix(prefixTenant), isPrefix: true);
        await AddRequestAsync("odr_dead", keyTenant, deadKey, isPrefix: false, dead: true);
        await using var services = BuildServices();

        // Act
        await StartAsync(services);

        // Assert
        (await services.GetRequiredService<BackWaveMonitor>().ListJobsAsync()).Count.ShouldBe(3);
        (await StoredJobs.QueuedAsync<DeleteObject>(services, keyTenant))
            .ShouldBe([new DeleteObject(keyTenant, key), new DeleteObject(keyTenant, deadKey)], ignoreOrder: true);
        (await StoredJobs.QueuedAsync<DeleteTenantObjects>(services, prefixTenant))
            .ShouldBe([new DeleteTenantObjects(prefixTenant)]);
        (await TableExistsAsync()).ShouldBeFalse();
        await using var scope = services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetPendingMigrationsAsync())
            .ShouldBeEmpty();
    }

    [Test]
    public async Task a_second_start_moves_no_row_again()
    {
        // Arrange
        await StartAtThePreviousMigrationAsync();
        var tenant = EntityId.New(Tenant.IdPrefix);
        await AddRequestAsync("odr_key", tenant, $"{StoredFile.TenantPrefix(tenant)}attachments/live", isPrefix: false);
        await using var services = BuildServices();
        await StartAsync(services);

        // Act
        await StartAsync(services);

        // Assert
        (await services.GetRequiredService<BackWaveMonitor>().ListJobsAsync()).Count.ShouldBe(1);
    }

    [Test]
    public async Task start_drops_a_row_that_fails_the_check_of_its_job_and_moves_the_others()
    {
        // Arrange
        await StartAtThePreviousMigrationAsync();
        var tenant = EntityId.New(Tenant.IdPrefix);
        var key = $"{StoredFile.TenantPrefix(tenant)}attachments/live";
        await AddRequestAsync("odr_key", tenant, key, isPrefix: false);
        await AddRequestAsync("odr_other_tenant", tenant,
            $"{StoredFile.TenantPrefix(EntityId.New(Tenant.IdPrefix))}attachments/live", isPrefix: false);
        // The job of a prefix deletes all the objects of the tenant, which is more than this prefix
        await AddRequestAsync("odr_narrow_prefix", tenant, $"{StoredFile.TenantPrefix(tenant)}attachments/", isPrefix: true);
        await AddRequestAsync("odr_no_tenant", "not_a_tenant", "tenants/not_a_tenant/", isPrefix: true);
        await using var services = BuildServices();

        // Act
        await StartAsync(services);

        // Assert
        (await services.GetRequiredService<BackWaveMonitor>().ListJobsAsync()).Count.ShouldBe(1);
        (await StoredJobs.QueuedAsync<DeleteObject>(services, tenant)).ShouldBe([new DeleteObject(tenant, key)]);
        (await TableExistsAsync()).ShouldBeFalse();
    }

    private async Task StartAtThePreviousMigrationAsync()
    {
        await _database.StartAsync();
        await using var context = CreateContext();
        await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
    }

    private ServiceProvider BuildServices() => TestFixture.BuildServiceProvider(_ => { }, _database.ConnectionString);

    private static Task StartAsync(ServiceProvider services)
        => new MigrationHostedService(services.GetRequiredService<IServiceScopeFactory>(),
            services.GetRequiredService<IConfiguration>()).StartingAsync(CancellationToken.None);

    private async Task AddRequestAsync(string id, string tenantId, string key, bool isPrefix, bool dead = false)
    {
        await using var connection = new NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            INSERT INTO object_delete_requests (id, tenant_id, key, is_prefix, requested_at, due_at, attempts, dead_at)
            VALUES (@id, @tenantId, @key, @isPrefix, now(), now(), 0, CASE WHEN @dead THEN now() END);
            """, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("tenantId", tenantId);
        command.Parameters.AddWithValue("key", key);
        command.Parameters.AddWithValue("isPrefix", isPrefix);
        command.Parameters.AddWithValue("dead", dead);
        await command.ExecuteNonQueryAsync();
    }

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_database.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AppDbContext(options, TimeProvider.System, Substitute.For<ICurrentUserService>(),
            Substitute.For<ICallerIdentityResolver>(), Substitute.For<ITenantScopeProvider>(),
            new DomainEventDispatcher(new ServiceCollection().BuildServiceProvider()), Substitute.For<IJobWriter>());
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<bool> TableExistsAsync()
    {
        await using var connection = new NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT to_regclass('object_delete_requests') IS NOT NULL", connection);
        return (bool)(await command.ExecuteScalarAsync())!;
    }
}
