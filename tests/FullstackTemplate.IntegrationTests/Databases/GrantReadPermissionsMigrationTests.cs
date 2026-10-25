namespace FullstackTemplate.IntegrationTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Services;
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
/// The data migration runs on a database of its own, from the migration before it, with users that existed before
/// read_users and read_agents: admins, users that were given a manage permission, and agents whose grant has a manage
/// permission. It also covers
/// <see cref="PermissionMigrationExtensions.GrantPermissionToHoldersOf"/> on the latest schema.
/// </summary>
public sealed class GrantReadPermissionsMigrationTests : IAsyncDisposable
{
    private const string PreviousMigration = "20261024175042_DropWhoAndWhenColumns";
    private const string ThisMigration = "20261025000954_GrantReadPermissions";

    private readonly PostgresTestDatabase _database = new();

    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    [Test]
    public async Task up_gives_the_read_permissions_to_the_admins_and_the_holders_and_a_second_run_changes_nothing()
    {
        // Arrange
        await _database.StartAsync();
        await using var context = CreateContext();
        await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
        await ExecuteAsync(ArrangeSql);

        // Act
        await context.GetService<IMigrator>().MigrateAsync(ThisMigration);
        var afterFirstRun = await ReadRowsAsync();
        foreach (var sql in UpSqlOf(context))
            await ExecuteAsync(sql);
        var afterSecondRun = await ReadRowsAsync();

        // Assert
        UsersWith(afterFirstRun, "read_users")
            .ShouldBe(["usr_admin", "usr_bareadmin", "usr_usermanager", "usr_accessmanager"], ignoreOrder: true);
        UsersWith(afterFirstRun, "read_agents")
            .ShouldBe(["usr_admin", "usr_bareadmin", "usr_agentmanager"], ignoreOrder: true);
        AgentsWith(afterFirstRun, "read_users").ShouldBe(["agt_usermanager", "agt_accessmanager"], ignoreOrder: true);
        AgentsWith(afterFirstRun, "read_agents").ShouldBe(["agt_agentmanager"]);
        afterSecondRun.ShouldBe(afterFirstRun, ignoreOrder: true);
    }

    [Test]
    public async Task down_deletes_the_read_permissions_and_keeps_the_other_rows()
    {
        // Arrange
        await _database.StartAsync();
        await using var context = CreateContext();
        await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
        await ExecuteAsync(ArrangeSql);
        var before = await ReadRowsAsync();
        await context.GetService<IMigrator>().MigrateAsync(ThisMigration);

        // Act
        await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);

        // Assert
        (await ReadRowsAsync()).ShouldBe(before, ignoreOrder: true);
    }

    private const string ArrangeSql = """
        INSERT INTO tenants (id, name, is_deleted) VALUES ('ten_migration', 'Migration tenant', false);
        INSERT INTO users (id, tenant_id, first_name, last_name, identifier, username, email, role, is_deleted) VALUES
            ('usr_admin', 'ten_migration', 'Ada', 'Admin', 'idp-admin', 'ada', 'ada@example.com', 'Admin', false),
            ('usr_bareadmin', 'ten_migration', 'Bea', 'Bare', 'idp-bareadmin', 'bea', 'bea@example.com', 'Admin', false),
            ('usr_usermanager', 'ten_migration', 'Uma', 'Users', 'idp-usermanager', 'uma', 'uma@example.com', 'User', false),
            ('usr_accessmanager', 'ten_migration', 'Abe', 'Access', 'idp-accessmanager', 'abe', 'abe@example.com', 'User', false),
            ('usr_agentmanager', 'ten_migration', 'Gus', 'Agents', 'idp-agentmanager', 'gus', 'gus@example.com', 'User', false),
            ('usr_revokedaccess', 'ten_migration', 'Rex', 'Revoked', 'idp-revokedaccess', 'rex', 'rex@example.com', 'User', false),
            ('usr_member', 'ten_migration', 'Mia', 'Member', 'idp-member', 'mia', 'mia@example.com', 'User', false),
            ('usr_deletedadmin', 'ten_migration', 'Dan', 'Deleted', 'idp-deletedadmin', 'dan', 'dan@example.com', 'Admin', true);
        INSERT INTO user_permissions (id, user_id, permission, is_deleted) VALUES
            ('uperm_admin1', 'usr_admin', 'manage_users', false),
            ('uperm_admin2', 'usr_admin', 'manage_user_access', false),
            ('uperm_admin3', 'usr_admin', 'manage_tenant', false),
            ('uperm_admin4', 'usr_admin', 'manage_agents', false),
            ('uperm_admin5', 'usr_admin', 'read_audit_log', false),
            ('uperm_admin6', 'usr_admin', 'read_audit_log_sensitive', false),
            ('uperm_usermanager', 'usr_usermanager', 'manage_users', false),
            ('uperm_accessmanager', 'usr_accessmanager', 'manage_user_access', false),
            ('uperm_agentmanager', 'usr_agentmanager', 'manage_agents', false),
            ('uperm_revokedaccess', 'usr_revokedaccess', 'manage_user_access', true),
            ('uperm_deletedadmin', 'usr_deletedadmin', 'manage_users', false);
        INSERT INTO agents (id, tenant_id, name, client_id, mode, is_deleted) VALUES
            ('agt_usermanager', 'ten_migration', 'User manager', 'client-usermanager', 'machine', false),
            ('agt_accessmanager', 'ten_migration', 'Access manager', 'client-accessmanager', 'delegated', false),
            ('agt_agentmanager', 'ten_migration', 'Agent manager', 'client-agentmanager', 'machine', false),
            ('agt_revokedaccess', 'ten_migration', 'Revoked', 'client-revokedaccess', 'machine', false),
            ('agt_tenantmanager', 'ten_migration', 'Tenant manager', 'client-tenantmanager', 'machine', false),
            ('agt_deleted', 'ten_migration', 'Deleted', 'client-deleted', 'machine', true);
        INSERT INTO agent_permissions (id, agent_id, permission, is_deleted) VALUES
            ('aperm_usermanager', 'agt_usermanager', 'manage_users', false),
            ('aperm_accessmanager', 'agt_accessmanager', 'manage_user_access', false),
            ('aperm_agentmanager', 'agt_agentmanager', 'manage_agents', false),
            ('aperm_revokedaccess', 'agt_revokedaccess', 'manage_users', true),
            ('aperm_tenantmanager', 'agt_tenantmanager', 'manage_tenant', false),
            ('aperm_deleted', 'agt_deleted', 'manage_agents', false);
        """;

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_database.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AppDbContext(options, TimeProvider.System, Substitute.For<ICurrentUserService>(),
            Substitute.For<ICallerIdentityResolver>(), Substitute.For<ITenantScopeProvider>(), new DomainEventDispatcher(new ServiceCollection().BuildServiceProvider()));
    }

    private static IEnumerable<string> UpSqlOf(AppDbContext context)
    {
        var migrations = context.GetService<IMigrationsAssembly>();
        var migration = migrations.CreateMigration(migrations.Migrations[ThisMigration], context.Database.ProviderName!);
        return migration.UpOperations.Cast<SqlOperation>().Select(o => o.Sql);
    }

    private static List<string> UsersWith(List<PermissionRow> rows, string permission)
        => OwnersWith(rows, "user_permissions", permission);

    private static List<string> AgentsWith(List<PermissionRow> rows, string permission)
        => OwnersWith(rows, "agent_permissions", permission);

    private static List<string> OwnersWith(List<PermissionRow> rows, string table, string permission)
        => rows.Where(r => r.Table == table && r.Permission == permission && !r.IsDeleted)
            .Select(r => r.OwnerId).ToList();

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<List<PermissionRow>> ReadRowsAsync()
    {
        await using var connection = new NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT 'user_permissions', id, user_id, permission, is_deleted FROM user_permissions
            UNION ALL
            SELECT 'agent_permissions', id, agent_id, permission, is_deleted FROM agent_permissions
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<PermissionRow>();
        while (await reader.ReadAsync())
        {
            rows.Add(new PermissionRow(reader.GetString(0), reader.GetString(1), reader.GetString(2),
                reader.GetString(3), reader.GetBoolean(4)));
        }
        return rows;
    }

    private sealed record PermissionRow(string Table, string Id, string OwnerId, string Permission, bool IsDeleted);
}
