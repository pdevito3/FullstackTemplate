using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FullstackTemplate.Server.Databases.Migrations
{
    /// <inheritdoc />
    public partial class GrantReadPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.GrantPermissionToRole("Admin", "read_users");
            migrationBuilder.GrantPermissionToRole("Admin", "read_agents");
            migrationBuilder.GrantPermissionToHoldersOf("read_users", "manage_users", "manage_user_access");
            migrationBuilder.GrantPermissionToHoldersOf("read_agents", "manage_agents");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeletePermission("read_agents");
            migrationBuilder.DeletePermission("read_users");
        }
    }
}
