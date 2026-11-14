namespace FullstackTemplate.IntegrationTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Resources.Jobs;
using FullstackTemplate.SharedTestHelpers.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NSubstitute;
using Shouldly;

/// <summary>
/// The drop of the old object delete queue runs on a database of its own, from the migration before it. A row in the
/// queue names an object that nothing else deletes, so the migration refuses to drop a queue with rows.
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
            "object_delete_requests has rows. Run the previous version until it drains them, and handle its dead rows.");
        (await TableExistsAsync()).ShouldBeTrue();
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
