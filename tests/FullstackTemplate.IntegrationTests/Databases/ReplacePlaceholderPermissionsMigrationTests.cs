namespace FullstackTemplate.IntegrationTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Services;
using FullstackTemplate.SharedTestHelpers.Utilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;
using NSubstitute;
using Shouldly;

/// <summary>
/// The data migration runs on a database of its own, from the migration before it, with rows that only an
/// existing database can have: the placeholder permission, live and soft-deleted.
/// </summary>
public sealed class ReplacePlaceholderPermissionsMigrationTests : IAsyncDisposable
{
    private const string PreviousMigration = "20261022195039_AddXminConcurrencyTokens";
    private const string ThisMigration = "20261023131355_ReplacePlaceholderPermissions";
    private static readonly string[] NewPermissions = ["manage_users", "manage_user_access", "manage_tenant"];

    private readonly PostgresTestDatabase _database = new();

    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    [Test]
    public async Task up_replaces_the_placeholder_with_the_admin_permissions_and_a_second_run_changes_nothing()
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
            await context.Database.ExecuteSqlRawAsync(sql);
        var afterSecondRun = await ReadRowsAsync();

        // Assert
        afterFirstRun.ShouldNotContain(r => r.Permission == "do_something_special");
        afterFirstRun.ShouldNotContain(r => r.UserId == "usr_member" || r.UserId == "usr_deletedadmin");

        var admin = afterFirstRun.Where(r => r.UserId == "usr_admin").ToList();
        admin.ShouldAllBe(r => !r.IsDeleted && r.CreatedBy == "migration");
        admin.Select(r => r.Permission).ShouldBe(NewPermissions, ignoreOrder: true);
        foreach (var row in admin)
        {
            EntityId.IsValid(row.Id, UserPermission.IdPrefix).ShouldBeTrue();
            var uuid = Guid.ParseExact(row.Id[(UserPermission.IdPrefix.Length + 1)..], "N");
            uuid.Version.ShouldBe(7);
            // The RFC 9562 variant is 10 in the two high bits.
            (uuid.Variant >> 2).ShouldBe(0b10);
        }

        var otherAdmin = afterFirstRun.Where(r => r.UserId == "usr_otheradmin").ToList();
        otherAdmin.Select(r => r.Permission).ShouldBe(NewPermissions, ignoreOrder: true);
        otherAdmin.ShouldContain(r => r.Id == "uperm_kept" && r.Permission == "manage_users");

        afterSecondRun.ShouldBe(afterFirstRun, ignoreOrder: true);
    }

    [Test]
    public async Task down_gives_the_placeholder_back_to_each_admin()
    {
        // Arrange
        await _database.StartAsync();
        await using var context = CreateContext();
        await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
        await ExecuteAsync(ArrangeSql);
        await context.GetService<IMigrator>().MigrateAsync(ThisMigration);

        // Act
        await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);

        // Assert
        var rows = await ReadRowsAsync();
        rows.ShouldNotContain(r => NewPermissions.Contains(r.Permission));
        rows.Where(r => !r.IsDeleted).Select(r => (r.UserId, r.Permission))
            .ShouldBe([("usr_admin", "do_something_special"), ("usr_otheradmin", "do_something_special")], ignoreOrder: true);
    }

    private const string ArrangeSql = """
        INSERT INTO tenants (id, name, created_on, is_deleted) VALUES ('ten_migration', 'Migration tenant', now(), false);
        INSERT INTO users (id, tenant_id, first_name, last_name, identifier, username, email, role, created_on, is_deleted) VALUES
            ('usr_admin', 'ten_migration', 'Ada', 'Admin', 'idp-admin', 'ada', 'ada@example.com', 'Admin', now(), false),
            ('usr_otheradmin', 'ten_migration', 'Otto', 'Admin', 'idp-otheradmin', 'otto', 'otto@example.com', 'Admin', now(), false),
            ('usr_member', 'ten_migration', 'Mia', 'Member', 'idp-member', 'mia', 'mia@example.com', 'User', now(), false),
            ('usr_deletedadmin', 'ten_migration', 'Dan', 'Deleted', 'idp-deletedadmin', 'dan', 'dan@example.com', 'Admin', now(), true);
        INSERT INTO user_permissions (id, user_id, permission, created_on, is_deleted) VALUES
            ('uperm_live', 'usr_admin', 'do_something_special', now(), false),
            ('uperm_softdeleted', 'usr_admin', 'do_something_special', now(), true),
            ('uperm_kept', 'usr_otheradmin', 'manage_users', now(), false),
            ('uperm_member', 'usr_member', 'do_something_special', now(), false);
        """;

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_database.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AppDbContext(options, TimeProvider.System, Substitute.For<ICurrentUserService>(),
            Substitute.For<ITenantScopeProvider>(), Substitute.For<IMediator>());
    }

    private static IEnumerable<string> UpSqlOf(AppDbContext context)
    {
        var migrations = context.GetService<IMigrationsAssembly>();
        var migration = migrations.CreateMigration(migrations.Migrations[ThisMigration], context.Database.ProviderName!);
        return migration.UpOperations.Cast<SqlOperation>().Select(o => o.Sql);
    }

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
            "SELECT id, user_id, permission, created_by, is_deleted FROM user_permissions", connection);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<PermissionRow>();
        while (await reader.ReadAsync())
        {
            rows.Add(new PermissionRow(reader.GetString(0), reader.GetString(1), reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetBoolean(4)));
        }
        return rows;
    }

    private sealed record PermissionRow(string Id, string UserId, string Permission, string? CreatedBy, bool IsDeleted);
}
