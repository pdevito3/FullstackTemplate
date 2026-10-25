namespace FullstackTemplate.Server.Databases;

using Microsoft.EntityFrameworkCore.Migrations;

/// <summary>
/// Data steps for the rows of <c>user_permissions</c> and <c>agent_permissions</c>. A migration is fixed in time, so
/// pass the role and the permission as string literals, not as <c>UserRole</c> or <c>Permission</c> factories.
/// </summary>
public static class PermissionMigrationExtensions
{
    private static readonly PermissionTable UserPermissions = new("user_permissions", "uperm", "users", "user_id");
    private static readonly PermissionTable AgentPermissions = new("agent_permissions", "aperm", "agents", "agent_id");

    /// <summary>
    /// Gives the permission to each live user with the role who has no live row for it. A second run changes nothing.
    /// </summary>
    public static void GrantPermissionToRole(this MigrationBuilder migrationBuilder, string role, string permission)
        => migrationBuilder.GrantPermission(UserPermissions, permission, $"o.role = {Literal(role)}");

    /// <summary>
    /// Gives the permission to each live user and to each live agent that has a live row for
    /// <paramref name="heldPermission"/> or one of <paramref name="otherHeldPermissions"/>, and no live row for the
    /// permission. For an agent, the rows are its grant. A second run changes nothing.
    /// </summary>
    public static void GrantPermissionToHoldersOf(this MigrationBuilder migrationBuilder, string permission,
        string heldPermission, params string[] otherHeldPermissions)
    {
        var held = string.Join(", ", otherHeldPermissions.Prepend(heldPermission).Select(Literal));
        foreach (var table in new[] { UserPermissions, AgentPermissions })
        {
            migrationBuilder.GrantPermission(table, permission, $"""
                EXISTS (
                      SELECT 1 FROM {table.Name} h
                      WHERE h.{table.OwnerColumn} = o.id AND NOT h.is_deleted AND h.permission IN ({held}))
                """);
        }
    }

    /// <summary>
    /// Hard-deletes each row of the permission in both tables, also the soft-deleted rows. Before the migration that
    /// adds the agents, only <c>user_permissions</c> exists.
    /// </summary>
    public static void DeletePermission(this MigrationBuilder migrationBuilder, string permission)
        => migrationBuilder.Sql($"""
            DELETE FROM user_permissions WHERE permission = {Literal(permission)};
            DO $delete$
            BEGIN
                IF to_regclass('agent_permissions') IS NOT NULL THEN
                    DELETE FROM agent_permissions WHERE permission = {Literal(permission)};
                END IF;
            END $delete$;
            """);

    /// <summary>
    /// Gives the permission to each live owner that matches <paramref name="ownerCondition"/>, a SQL condition on the
    /// alias <c>o</c> of the owner table, and has no live row for the permission.
    /// </summary>
    private static void GrantPermission(
        this MigrationBuilder migrationBuilder, PermissionTable table, string permission, string ownerCondition)
    {
        var ownersWithoutIt = $"""
            FROM {table.Owners} o
            WHERE {ownerCondition}
              AND NOT o.is_deleted
              AND NOT EXISTS (
                  SELECT 1 FROM {table.Name} p
                  WHERE p.{table.OwnerColumn} = o.id AND p.permission = {Literal(permission)} AND NOT p.is_deleted)
            """;

        // The same ID as EntityId.New: the prefix and a UUIDv7 in hex. Postgres 17 has no uuidv7(), so this puts the
        // Unix time in milliseconds in the first 48 bits of gen_random_uuid(), and bits 52 and 53 change its version
        // nibble from 4 to 7.
        var newId = $"""
            '{table.IdPrefix}_' || encode(set_bit(set_bit(overlay(uuid_send(gen_random_uuid())
                placing substring(int8send(floor(extract(epoch from clock_timestamp()) * 1000)::bigint) from 3)
                from 1 for 6), 52, 1), 53, 1), 'hex')
            """;

        // The migrations before DropWhoAndWhenColumns call this too. Then created_on exists, is NOT NULL, and has no
        // default. PL/pgSQL checks the columns of a statement only when it runs it, so the branch that names
        // created_on does no harm after the drop.
        migrationBuilder.Sql($"""
            DO $grant$
            BEGIN
                IF EXISTS (SELECT 1 FROM pg_attribute
                           WHERE attrelid = '{table.Name}'::regclass AND attname = 'created_on' AND NOT attisdropped) THEN
                    INSERT INTO {table.Name} (id, {table.OwnerColumn}, permission, created_on, is_deleted)
                    SELECT {newId}, o.id, {Literal(permission)}, now(), false
                    {ownersWithoutIt};
                ELSE
                    INSERT INTO {table.Name} (id, {table.OwnerColumn}, permission, is_deleted)
                    SELECT {newId}, o.id, {Literal(permission)}, false
                    {ownersWithoutIt};
                END IF;
            END $grant$;
            """);
    }

    private static string Literal(string value) => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";

    /// <param name="Name">The table of the permission rows.</param>
    /// <param name="IdPrefix">The ID prefix of a row.</param>
    /// <param name="Owners">The table of the users or the agents that own the rows.</param>
    /// <param name="OwnerColumn">The column of a row that holds the ID of its owner.</param>
    private sealed record PermissionTable(string Name, string IdPrefix, string Owners, string OwnerColumn);
}
