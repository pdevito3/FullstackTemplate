using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FullstackTemplate.Server.Databases.Migrations
{
    /// <summary>
    /// Removes the do_something_special placeholder and gives each admin the permissions that replace it.
    /// The model does not change.
    /// </summary>
    public partial class ReplacePlaceholderPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeletePermission("do_something_special");
            migrationBuilder.GrantPermissionToRole("Admin", "manage_users");
            migrationBuilder.GrantPermissionToRole("Admin", "manage_user_access");
            migrationBuilder.GrantPermissionToRole("Admin", "manage_tenant");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeletePermission("manage_users");
            migrationBuilder.DeletePermission("manage_user_access");
            migrationBuilder.DeletePermission("manage_tenant");
            migrationBuilder.GrantPermissionToRole("Admin", "do_something_special");
        }
    }
}
