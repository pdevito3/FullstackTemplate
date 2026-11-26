namespace FullstackTemplate.IntegrationTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Resources.Jobs;
using FullstackTemplate.SharedTestHelpers.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NSubstitute;
using Shouldly;

/// <summary>
/// A future data migration calls <see cref="PermissionMigrationExtensions.GrantPermissionToRole"/> or
/// <see cref="PermissionMigrationExtensions.GrantPermissionToManagers"/> on the schema of today, which has no
/// <c>created_on</c>. <see cref="ReplacePlaceholderPermissionsMigrationTests"/> covers the schema
/// before the drop.
/// </summary>
public sealed class GrantPermissionMigrationTests : IAsyncDisposable
{
    private readonly PostgresTestDatabase _database = new();

    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    [Test]
    public async Task grant_on_the_latest_schema_gives_the_permission_to_each_live_user_with_the_role_once()
    {
        // Arrange
        await _database.StartAsync();
        await using var context = CreateContext();
        await context.GetService<IMigrator>().MigrateAsync();
        await ExecuteAsync(ArrangeSql);
        var builder = new MigrationBuilder(context.Database.ProviderName);
        builder.GrantPermissionToRole("Admin", "manage_agents");

        // Act
        foreach (var sql in builder.Operations.Cast<SqlOperation>().Select(o => o.Sql))
        {
            await ExecuteAsync(sql);
            await ExecuteAsync(sql);
        }

        // Assert
        var granted = await UsersWithAsync("manage_agents");
        granted.ShouldBe(["usr_admin"]);
        (await OverridesAsync("manage_agents")).ShouldBe([("usr_removedadmin", false)]);
    }

    [Test]
    [Property("Rule", "UM-4")]
    public async Task grant_to_managers_gives_the_permission_to_each_admin_and_each_user_who_holds_every_permission_once()
    {
        // Arrange
        await _database.StartAsync();
        await using var context = CreateContext();
        await context.GetService<IMigrator>().MigrateAsync();
        await ExecuteAsync(ArrangeSql);
        await ExecuteAsync(ManagersSql);
        var builder = new MigrationBuilder(context.Database.ProviderName);
        builder.GrantPermissionToManagers("manage_agents");

        // Act
        foreach (var sql in builder.Operations.Cast<SqlOperation>().Select(o => o.Sql))
        {
            await ExecuteAsync(sql);
            await ExecuteAsync(sql);
        }

        // Assert
        var granted = await UsersWithAsync("manage_agents");
        granted.Order().ShouldBe(["usr_admin", "usr_manager"]);
        // No role of the manager gives the permission, so an override keeps it after a later change of the roles
        (await OverridesAsync("manage_agents")).Order().ShouldBe([("usr_manager", true), ("usr_removedadmin", false)]);
    }

    [Test]
    public async Task grant_to_holders_gives_the_permission_and_a_granted_override_and_keeps_a_removed_one()
    {
        // Arrange
        await _database.StartAsync();
        await using var context = CreateContext();
        await context.GetService<IMigrator>().MigrateAsync();
        await ExecuteAsync(ArrangeSql);
        await ExecuteAsync(ManagersSql);
        await ExecuteAsync("""
            INSERT INTO user_permissions (id, user_id, permission, is_deleted) VALUES
                ('uperm_removedadmin1', 'usr_removedadmin', 'manage_users', false);
            """);
        var builder = new MigrationBuilder(context.Database.ProviderName);
        builder.GrantPermissionToHoldersOf("manage_agents", "manage_users");

        // Act
        foreach (var sql in builder.Operations.Cast<SqlOperation>().Select(o => o.Sql))
        {
            await ExecuteAsync(sql);
            await ExecuteAsync(sql);
        }

        // Assert: the member's manage_users row is deleted, so the member holds nothing to grant from
        (await UsersWithAsync("manage_agents")).Order().ShouldBe(["usr_admin", "usr_manager"]);
        (await OverridesAsync("manage_agents")).Order()
            .ShouldBe([("usr_admin", true), ("usr_manager", true), ("usr_removedadmin", false)]);
    }

    [Test]
    public async Task delete_permission_also_deletes_its_overrides()
    {
        // Arrange
        await _database.StartAsync();
        await using var context = CreateContext();
        await context.GetService<IMigrator>().MigrateAsync();
        await ExecuteAsync(ArrangeSql);
        await ExecuteAsync("""
            INSERT INTO user_permissions (id, user_id, permission, is_deleted) VALUES
                ('uperm_admin1', 'usr_admin', 'manage_agents', false);
            INSERT INTO user_permission_overrides (id, user_id, permission, granted, is_deleted) VALUES
                ('upovr_member', 'usr_member', 'manage_agents', true, true);
            """);
        var builder = new MigrationBuilder(context.Database.ProviderName);
        builder.DeletePermission("manage_agents");

        // Act
        foreach (var sql in builder.Operations.Cast<SqlOperation>().Select(o => o.Sql))
            await ExecuteAsync(sql);

        // Assert: a row of a permission that Permission does not know would stop each load of its user
        (await UsersWithAsync("manage_agents")).ShouldBeEmpty();
        (await CountAsync("SELECT count(*) FROM user_permission_overrides WHERE permission = 'manage_agents'")).ShouldBe(0);
    }

    private const string ArrangeSql = """
        INSERT INTO tenants (id, name, is_deleted) VALUES ('ten_migration', 'Migration tenant', false);
        INSERT INTO users (id, tenant_id, first_name, last_name, identifier, username, email, is_deleted) VALUES
            ('usr_admin', 'ten_migration', 'Ada', 'Admin', 'idp-admin', 'ada', 'ada@example.com', false),
            ('usr_member', 'ten_migration', 'Mia', 'Member', 'idp-member', 'mia', 'mia@example.com', false),
            ('usr_deletedadmin', 'ten_migration', 'Dan', 'Deleted', 'idp-deletedadmin', 'dan', 'dan@example.com', true),
            ('usr_removedadmin', 'ten_migration', 'Rae', 'Removed', 'idp-removedadmin', 'rae', 'rae@example.com', false);
        INSERT INTO user_roles (id, user_id, role, is_deleted) VALUES
            ('urole_admin', 'usr_admin', 'Admin', false),
            ('urole_member', 'usr_member', 'User', false),
            ('urole_deletedadmin', 'usr_deletedadmin', 'Admin', true),
            ('urole_removedadmin', 'usr_removedadmin', 'Admin', false);
        -- Someone removed the permission from this admin by hand, so the grant leaves it removed
        INSERT INTO user_permission_overrides (id, user_id, permission, granted, is_deleted) VALUES
            ('upovr_removedadmin', 'usr_removedadmin', 'manage_agents', false, false);
        """;

    private const string ManagersSql = """
        INSERT INTO users (id, tenant_id, first_name, last_name, identifier, username, email, is_deleted) VALUES
            ('usr_manager', 'ten_migration', 'Max', 'Manager', 'idp-manager', 'max', 'max@example.com', false);
        INSERT INTO user_roles (id, user_id, role, is_deleted) VALUES ('urole_manager', 'usr_manager', 'User', false);
        INSERT INTO user_permissions (id, user_id, permission, is_deleted) VALUES
            ('uperm_admin1', 'usr_admin', 'manage_users', false),
            ('uperm_admin2', 'usr_admin', 'read_users', false),
            ('uperm_manager1', 'usr_manager', 'manage_users', false),
            ('uperm_manager2', 'usr_manager', 'read_users', false),
            ('uperm_member1', 'usr_member', 'read_users', false),
            ('uperm_member2', 'usr_member', 'manage_users', true);
        """;

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_database.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AppDbContext(options, TimeProvider.System, Substitute.For<ICurrentUserService>(),
            Substitute.For<ICallerIdentityResolver>(), Substitute.For<ITenantScopeProvider>(), new DomainEventDispatcher(new ServiceCollection().BuildServiceProvider()),
            Substitute.For<IJobWriter>());
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<long> CountAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private async Task<List<(string UserId, bool Granted)>> OverridesAsync(string permission)
    {
        await using var connection = new NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT user_id, granted FROM user_permission_overrides WHERE permission = @permission AND NOT is_deleted",
            connection);
        command.Parameters.AddWithValue("permission", permission);
        await using var reader = await command.ExecuteReaderAsync();
        var overrides = new List<(string, bool)>();
        while (await reader.ReadAsync())
            overrides.Add((reader.GetString(0), reader.GetBoolean(1)));
        return overrides;
    }

    private async Task<List<string>> UsersWithAsync(string permission)
    {
        await using var connection = new NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT user_id FROM user_permissions WHERE permission = @permission AND NOT is_deleted", connection);
        command.Parameters.AddWithValue("permission", permission);
        await using var reader = await command.ExecuteReaderAsync();
        var userIds = new List<string>();
        while (await reader.ReadAsync())
            userIds.Add(reader.GetString(0));
        return userIds;
    }
}
