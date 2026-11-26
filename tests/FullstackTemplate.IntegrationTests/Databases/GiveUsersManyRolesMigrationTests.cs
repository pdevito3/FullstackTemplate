namespace FullstackTemplate.IntegrationTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain;
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
/// The move from one role on the user to many role rows runs on a database of its own, from the migration before it,
/// with a user who holds a permission that the role does not give and an admin who lacks one that the role gives.
/// </summary>
public sealed class GiveUsersManyRolesMigrationTests : IAsyncDisposable
{
    private const string ThisMigration = "20261125192501_GiveUsersManyRoles";

    private readonly PostgresTestDatabase _database = new();

    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    [Test]
    public async Task up_gives_each_user_their_role_row_and_keeps_their_permissions_with_overrides()
    {
        // Arrange
        await _database.StartAsync();
        await using var context = CreateContext();
        // The migration before this one, by ID. A hard-coded name could be an example migration, which an app with no
        // examples does not have.
        var previousMigration = context.GetService<IMigrationsAssembly>().Migrations.Keys
            .Where(id => string.CompareOrdinal(id, ThisMigration) < 0)
            .Max(StringComparer.Ordinal);
        await context.GetService<IMigrator>().MigrateAsync(previousMigration);
        await ExecuteAsync(ArrangeSql());
        var permissionsBefore = await ReadAsync("SELECT user_id, permission, is_deleted FROM user_permissions",
            r => (r.GetString(0), r.GetString(1), r.GetBoolean(2)));

        // Act
        await context.GetService<IMigrator>().MigrateAsync(ThisMigration);

        // Assert
        var roles = await ReadAsync("SELECT id, user_id, role, is_deleted FROM user_roles",
            r => (Id: r.GetString(0), UserId: r.GetString(1), Role: r.GetString(2), IsDeleted: r.GetBoolean(3)));
        roles.Select(r => (r.UserId, r.Role, r.IsDeleted)).ShouldBe(
            [("usr_member", "User", false), ("usr_admin", "Admin", false), ("usr_deleted", "Admin", true),
             ("usr_gone", "User", true)],
            ignoreOrder: true);
        roles.ShouldAllBe(r => EntityId.IsValid(r.Id, UserRoleAssignment.IdPrefix));

        // Each live user holds what their roles give with their overrides, as ComputePermissions makes it
        var overrides = await ReadAsync("SELECT id, user_id, permission, granted FROM user_permission_overrides",
            r => (Id: r.GetString(0), UserId: r.GetString(1), Permission: r.GetString(2), Granted: r.GetBoolean(3)));
        foreach (var userId in new[] { "usr_member", "usr_admin" })
        {
            var given = UserRole.PermissionsOf(roles.Where(r => r.UserId == userId).Select(r => UserRole.Of(r.Role)))
                .Select(p => p.Value)
                .Union(overrides.Where(o => o.UserId == userId && o.Granted).Select(o => o.Permission))
                .Except(overrides.Where(o => o.UserId == userId && !o.Granted).Select(o => o.Permission));
            permissionsBefore.Where(p => p.Item1 == userId && !p.Item3).Select(p => p.Item2)
                .ShouldBe(given, ignoreOrder: true, customMessage: userId);
        }
        overrides.ShouldContain(o => o.UserId == "usr_member" && o.Permission == "read_users" && o.Granted);
        overrides.ShouldContain(o => o.UserId == "usr_admin" && o.Permission == "manage_tenant" && !o.Granted);
        overrides.ShouldNotContain(o => o.UserId == "usr_deleted" || o.UserId == "usr_gone");
        overrides.ShouldAllBe(o => EntityId.IsValid(o.Id, UserPermissionOverride.IdPrefix));
    }

    // The admin holds the permissions of this release but manage_agents and manage_tenant, and no permission of the
    // examples, which no other user holds either. Someone removed manage_tenant by hand, so only a soft-deleted row is
    // left for it. The member had manage_users removed the same way. The deleted admin holds nothing, and the deleted
    // member holds read_users: neither gets an override.
    private static string ArrangeSql()
    {
        string[] releasePermissions = ["manage_users", "manage_user_access", "manage_tenant", "manage_agents",
            "read_audit_log", "read_audit_log_sensitive", "read_users", "read_agents"];
        var adminPermissions = releasePermissions.Where(p => p is not ("manage_agents" or "manage_tenant"))
            .Select((p, i) => $"('uperm_admin{i}', 'usr_admin', '{p}', false)");
        return $"""
            INSERT INTO tenants (id, name, is_deleted) VALUES ('ten_migration', 'Migration tenant', false);
            INSERT INTO users (id, tenant_id, first_name, last_name, identifier, username, email, role, is_deleted) VALUES
                ('usr_member', 'ten_migration', 'Mia', 'Member', 'idp-member', 'mia', 'mia@example.com', 'User', false),
                ('usr_admin', 'ten_migration', 'Ada', 'Admin', 'idp-admin', 'ada', 'ada@example.com', 'Admin', false),
                ('usr_deleted', 'ten_migration', 'Dan', 'Deleted', 'idp-deleted', 'dan', 'dan@example.com', 'Admin', true),
                ('usr_gone', 'ten_migration', 'Gus', 'Gone', 'idp-gone', 'gus', 'gus@example.com', 'User', true);
            INSERT INTO user_permissions (id, user_id, permission, is_deleted) VALUES
                ('uperm_member', 'usr_member', 'read_users', false),
                ('uperm_memberremoved', 'usr_member', 'manage_users', true),
                ('uperm_adminremoved', 'usr_admin', 'manage_tenant', true),
                ('uperm_gone', 'usr_gone', 'read_users', false),
                {string.Join(",\n    ", adminPermissions)};
            """;
    }

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

    private async Task<List<T>> ReadAsync<T>(string sql, Func<NpgsqlDataReader, T> read)
    {
        await using var connection = new NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<T>();
        while (await reader.ReadAsync())
            rows.Add(read(reader));
        return rows;
    }
}
