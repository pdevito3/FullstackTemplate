using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FullstackTemplate.Server.Databases.Migrations
{
    /// <inheritdoc />
    public partial class GiveUsersManyRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_permission_overrides",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, collation: "C"),
                    user_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, collation: "C"),
                    granted = table.Column<bool>(type: "boolean", nullable: false),
                    permission = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_modified_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_permission_overrides", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_permission_overrides_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, collation: "C"),
                    user_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, collation: "C"),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    last_modified_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_roles", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_roles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_permission_overrides_user_id",
                table: "user_permission_overrides",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_roles_user_id",
                table: "user_roles",
                column: "user_id");

            // The ID of each new row is the same as EntityId.New: the prefix and a UUIDv7 in hex (see
            // PermissionMigrationExtensions).

            // Each user keeps their one role. A deleted user gets a deleted row.
            migrationBuilder.Sql("""
                INSERT INTO user_roles (id, user_id, role, is_deleted)
                SELECT 'urole_' || encode(set_bit(set_bit(overlay(uuid_send(gen_random_uuid())
                           placing substring(int8send(floor(extract(epoch from clock_timestamp()) * 1000)::bigint) from 3)
                           from 1 for 6), 52, 1), 53, 1), 'hex'),
                       u.id, u.role, u.is_deleted
                FROM users u;
                """);

            // The permissions of each live user stay the same. A permission that the role does not give becomes a
            // granted override, and a permission that the role gives and the user does not hold becomes a removed
            // override. The User role gives no permission. The Admin role gives each permission: the permissions of
            // this release, and each permission that a user holds. An app with other roles, or with other
            // permissions, edits these lists before it runs this migration.
            var adminPermissions = "'manage_users', 'manage_user_access', 'manage_tenant', 'manage_agents', "
                + "'read_audit_log', 'read_audit_log_sensitive', 'read_users', 'read_agents'";
            // example:start
            // The examples add manage_workshops. An admin who lacks it keeps lacking it, even when no user holds it.
            adminPermissions += ", 'manage_workshops'";
            // example:end
            migrationBuilder.Sql($$"""
                WITH admin_permissions AS (
                    SELECT unnest(ARRAY[{{adminPermissions}}]) AS permission
                    UNION
                    SELECT permission FROM user_permissions WHERE NOT is_deleted),
                overrides AS (
                    SELECT u.id AS user_id, p.permission, true AS granted
                    FROM users u
                    JOIN user_permissions p ON p.user_id = u.id AND NOT p.is_deleted
                    WHERE NOT u.is_deleted AND u.role <> 'Admin'
                    UNION
                    SELECT u.id, a.permission, false
                    FROM users u
                    CROSS JOIN admin_permissions a
                    WHERE NOT u.is_deleted AND u.role = 'Admin'
                      AND NOT EXISTS (
                          SELECT 1 FROM user_permissions p
                          WHERE p.user_id = u.id AND p.permission = a.permission AND NOT p.is_deleted))
                INSERT INTO user_permission_overrides (id, user_id, permission, granted, is_deleted)
                SELECT 'upovr_' || encode(set_bit(set_bit(overlay(uuid_send(gen_random_uuid())
                           placing substring(int8send(floor(extract(epoch from clock_timestamp()) * 1000)::bigint) from 3)
                           from 1 for 6), 52, 1), 53, 1), 'hex'),
                       o.user_id, o.permission, o.granted, false
                FROM overrides o;
                """);

            migrationBuilder.DropColumn(
                name: "role",
                table: "users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "role",
                table: "users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            // A user keeps one role: Admin when they have it, else User. The permission rows stay as they are.
            migrationBuilder.Sql("""
                UPDATE users u
                SET role = CASE WHEN EXISTS (
                        SELECT 1 FROM user_roles r WHERE r.user_id = u.id AND r.role = 'Admin' AND NOT r.is_deleted)
                    THEN 'Admin' ELSE 'User' END;
                """);

            migrationBuilder.DropTable(
                name: "user_permission_overrides");

            migrationBuilder.DropTable(
                name: "user_roles");
        }
    }
}
