namespace FullstackTemplate.Server.Databases;

using Microsoft.EntityFrameworkCore.Migrations;

/// <summary>
/// Data steps for the rows of <c>user_permissions</c>. A migration is fixed in time, so pass the role and the
/// permission as string literals, not as <c>UserRole</c> or <c>Permission</c> factories.
/// </summary>
public static class PermissionMigrationExtensions
{
    // The same ID as EntityId.New: the prefix and a UUIDv7 in hex. Postgres 17 has no uuidv7(), so this puts the
    // Unix time in milliseconds in the first 48 bits of gen_random_uuid(), and bits 52 and 53 change its version
    // nibble from 4 to 7.
    private const string NewUserPermissionId = """
        'uperm_' || encode(set_bit(set_bit(overlay(uuid_send(gen_random_uuid())
            placing substring(int8send(floor(extract(epoch from clock_timestamp()) * 1000)::bigint) from 3)
            from 1 for 6), 52, 1), 53, 1), 'hex')
        """;

    /// <summary>
    /// Gives the permission to each live user with the role who has no live row for it. A second run changes nothing.
    /// </summary>
    public static void GrantPermissionToRole(this MigrationBuilder migrationBuilder, string role, string permission)
        => migrationBuilder.Sql($"""
            INSERT INTO user_permissions (id, user_id, permission, created_on, created_by, is_deleted)
            SELECT {NewUserPermissionId}, u.id, {Literal(permission)}, now(), 'migration', false
            FROM users u
            WHERE u.role = {Literal(role)}
              AND NOT u.is_deleted
              AND NOT EXISTS (
                  SELECT 1 FROM user_permissions p
                  WHERE p.user_id = u.id AND p.permission = {Literal(permission)} AND NOT p.is_deleted);
            """);

    /// <summary>
    /// Hard-deletes each row of the permission, also the soft-deleted rows.
    /// </summary>
    public static void DeletePermission(this MigrationBuilder migrationBuilder, string permission)
        => migrationBuilder.Sql($"DELETE FROM user_permissions WHERE permission = {Literal(permission)};");

    private static string Literal(string value) => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
}
