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
/// A future data migration calls <see cref="PermissionMigrationExtensions.GrantPermissionToRole"/> on the schema of
/// today, which has no <c>created_on</c>. <see cref="ReplacePlaceholderPermissionsMigrationTests"/> covers the schema
/// before the drop.
/// </summary>
public sealed class GrantPermissionToRoleMigrationTests : IAsyncDisposable
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
    }

    private const string ArrangeSql = """
        INSERT INTO tenants (id, name, is_deleted) VALUES ('ten_migration', 'Migration tenant', false);
        INSERT INTO users (id, tenant_id, first_name, last_name, identifier, username, email, role, is_deleted) VALUES
            ('usr_admin', 'ten_migration', 'Ada', 'Admin', 'idp-admin', 'ada', 'ada@example.com', 'Admin', false),
            ('usr_member', 'ten_migration', 'Mia', 'Member', 'idp-member', 'mia', 'mia@example.com', 'User', false),
            ('usr_deletedadmin', 'ten_migration', 'Dan', 'Deleted', 'idp-deletedadmin', 'dan', 'dan@example.com', 'Admin', true);
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

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
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
