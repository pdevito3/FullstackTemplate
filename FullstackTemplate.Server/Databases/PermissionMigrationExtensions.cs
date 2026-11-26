namespace FullstackTemplate.Server.Databases;

using Microsoft.EntityFrameworkCore.Migrations;

/// <summary>
/// Data steps for the rows of <c>user_permissions</c> and <c>agent_permissions</c>. A migration is fixed in time, so
/// pass the role and the permission as string literals, not as <c>UserRole</c> or <c>Permission</c> factories.
/// </summary>
/// <remarks>
/// Before <c>GiveUsersManyRoles</c>, a user has one role in <c>users.role</c>. After it, the roles are rows of
/// <c>user_roles</c>, and <c>User</c> computes <c>user_permissions</c> again from the roles and the rows of
/// <c>user_permission_overrides</c> on each change. So on that schema a permission that no role gives must also get a
/// granted override, or the next change of the user takes it away. Each step reads the schema when it runs.
/// </remarks>
public static class PermissionMigrationExtensions
{
    private static readonly PermissionTable UserPermissions = new("user_permissions", "uperm", "users", "user_id");
    private static readonly PermissionTable AgentPermissions = new("agent_permissions", "aperm", "agents", "agent_id");

    /// <summary>
    /// Gives the permission to each live user with the role who has no live row for it. A second run changes nothing.
    /// </summary>
    public static void GrantPermissionToRole(this MigrationBuilder migrationBuilder, string role, string permission)
        => migrationBuilder.GrantPermission(UserPermissions, permission, new OwnerCondition(
            $"o.role = {Literal(role)}", HasRole(role)));

    /// <summary>
    /// Gives a new permission to each live user with the Admin role, and to each live user who holds each permission
    /// that a live user holds, who has no live row for it. The second set keeps each tenant manager of UM-4 a manager
    /// when that user has another role. A second run changes nothing.
    /// </summary>
    public static void GrantPermissionToManagers(this MigrationBuilder migrationBuilder, string permission)
    {
        var holdsEach = $"""
            NOT EXISTS (
                  SELECT a.permission FROM user_permissions a WHERE NOT a.is_deleted AND a.permission <> {Literal(permission)}
                  EXCEPT
                  SELECT h.permission FROM user_permissions h WHERE h.user_id = o.id AND NOT h.is_deleted)
            """;
        // The Admin role gives the permission in the code. A manager with no Admin role holds it by an override.
        migrationBuilder.GrantPermission(UserPermissions, permission, new OwnerCondition(
            $"(o.role = 'Admin' OR {holdsEach})",
            $"({HasRole("Admin")} OR {holdsEach})",
            $"(NOT {HasRole("Admin")} AND {holdsEach})"));
    }

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
            var holdsOne = $"""
                EXISTS (
                      SELECT 1 FROM {table.Name} h
                      WHERE h.{table.OwnerColumn} = o.id AND NOT h.is_deleted AND h.permission IN ({held}))
                """;
            // No role gives the permission to a user for holding another one, so a user holds it by an override
            migrationBuilder.GrantPermission(table, permission, table == UserPermissions
                ? new OwnerCondition(holdsOne, holdsOne, holdsOne)
                : new OwnerCondition(holdsOne, holdsOne));
        }
    }

    /// <summary>
    /// Hard-deletes each row of the permission in <c>user_permissions</c>, <c>agent_permissions</c>, and
    /// <c>user_permission_overrides</c>, also the soft-deleted rows. A row of a permission that <c>Permission</c> does
    /// not know stops each load of its owner. Before the migration that adds the agents, only <c>user_permissions</c>
    /// exists, and before <c>GiveUsersManyRoles</c>, the overrides do not.
    /// </summary>
    public static void DeletePermission(this MigrationBuilder migrationBuilder, string permission)
        => migrationBuilder.Sql($"""
            DELETE FROM user_permissions WHERE permission = {Literal(permission)};
            DO $delete$
            BEGIN
                IF to_regclass('agent_permissions') IS NOT NULL THEN
                    DELETE FROM agent_permissions WHERE permission = {Literal(permission)};
                END IF;
                IF to_regclass('user_permission_overrides') IS NOT NULL THEN
                    DELETE FROM user_permission_overrides WHERE permission = {Literal(permission)};
                END IF;
            END $delete$;
            """);

    /// <summary>
    /// Gives the permission to each live owner that matches the condition, and has no live row for the permission. On
    /// the schema with <c>user_roles</c>, a user with an override that removes the permission keeps it removed, and a
    /// user who matches <see cref="OwnerCondition.Override"/> also gets a granted override.
    /// </summary>
    private static void GrantPermission(
        this MigrationBuilder migrationBuilder, PermissionTable table, string permission, OwnerCondition condition)
    {
        string OwnersWithoutIt(string ownerCondition) => $"""
            FROM {table.Owners} o
            WHERE {ownerCondition}
              AND NOT o.is_deleted
              AND NOT EXISTS (
                  SELECT 1 FROM {table.Name} p
                  WHERE p.{table.OwnerColumn} = o.id AND p.permission = {Literal(permission)} AND NOT p.is_deleted)
            """;
        var withRoles = table == UserPermissions
            ? $"""
                {condition.WithRoles}
                  AND NOT EXISTS (
                      SELECT 1 FROM user_permission_overrides v
                      WHERE v.user_id = o.id AND v.permission = {Literal(permission)} AND NOT v.granted AND NOT v.is_deleted)
                """
            : condition.WithRoles;
        // Before the permission rows, because the condition reads the owners that have no row yet
        var overrides = condition.Override is null ? "" : $"""
            INSERT INTO user_permission_overrides (id, user_id, permission, granted, is_deleted)
                    SELECT {NewId("upovr")}, o.id, {Literal(permission)}, true, false
                    {OwnersWithoutIt(condition.Override)}
                      AND NOT EXISTS (
                          SELECT 1 FROM user_permission_overrides v
                          WHERE v.user_id = o.id AND v.permission = {Literal(permission)} AND NOT v.is_deleted);
            """;

        var newId = NewId(table.IdPrefix);

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
                    {OwnersWithoutIt(condition.BeforeRoles)};
                ELSIF to_regclass('user_roles') IS NULL THEN
                    INSERT INTO {table.Name} (id, {table.OwnerColumn}, permission, is_deleted)
                    SELECT {newId}, o.id, {Literal(permission)}, false
                    {OwnersWithoutIt(condition.BeforeRoles)};
                ELSE
                    {overrides}
                    INSERT INTO {table.Name} (id, {table.OwnerColumn}, permission, is_deleted)
                    SELECT {newId}, o.id, {Literal(permission)}, false
                    {OwnersWithoutIt(withRoles)};
                END IF;
            END $grant$;
            """);
    }

    private static string Literal(string value) => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";

    private static string HasRole(string role) => $"""
        EXISTS (SELECT 1 FROM user_roles r WHERE r.user_id = o.id AND r.role = {Literal(role)} AND NOT r.is_deleted)
        """;

    // The same ID as EntityId.New: the prefix and a UUIDv7 in hex. Postgres 17 has no uuidv7(), so this puts the
    // Unix time in milliseconds in the first 48 bits of gen_random_uuid(), and bits 52 and 53 change its version
    // nibble from 4 to 7.
    private static string NewId(string idPrefix) => $"""
        '{idPrefix}_' || encode(set_bit(set_bit(overlay(uuid_send(gen_random_uuid())
            placing substring(int8send(floor(extract(epoch from clock_timestamp()) * 1000)::bigint) from 3)
            from 1 for 6), 52, 1), 53, 1), 'hex')
        """;

    /// <summary>The owners that get the permission, as SQL conditions on the alias <c>o</c> of the owner table.</summary>
    /// <param name="BeforeRoles">On the schema with <c>users.role</c>.</param>
    /// <param name="WithRoles">On the schema with <c>user_roles</c>.</param>
    /// <param name="Override">On the schema with <c>user_roles</c>, the users whose permission no role gives. They also
    /// get a granted override. Null for an agent and for a grant that a role gives.</param>
    private sealed record OwnerCondition(string BeforeRoles, string WithRoles, string? Override = null);

    /// <param name="Name">The table of the permission rows.</param>
    /// <param name="IdPrefix">The ID prefix of a row.</param>
    /// <param name="Owners">The table of the users or the agents that own the rows.</param>
    /// <param name="OwnerColumn">The column of a row that holds the ID of its owner.</param>
    private sealed record PermissionTable(string Name, string IdPrefix, string Owners, string OwnerColumn);
}
