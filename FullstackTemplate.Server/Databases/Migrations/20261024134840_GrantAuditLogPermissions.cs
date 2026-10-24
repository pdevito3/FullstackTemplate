using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FullstackTemplate.Server.Databases.Migrations
{
    /// <inheritdoc />
    public partial class GrantAuditLogPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.GrantPermissionToRole("Admin", "read_audit_log");
            migrationBuilder.GrantPermissionToRole("Admin", "read_audit_log_sensitive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeletePermission("read_audit_log_sensitive");
            migrationBuilder.DeletePermission("read_audit_log");
        }
    }
}
